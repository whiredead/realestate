using FluentAssertions;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §7.2 — proves the media host policy is fail-closed.
///
/// Project images, site videos and 3D links are rendered straight into the
/// public catalogue, so an unvalidated string here is an injection point. These
/// tests exist because the dangerous cases all LOOK like valid URLs to a
/// well-meaning "is it a link?" check: <c>javascript:</c> and <c>data:</c>
/// parse as absolute URIs, and an <c>http://</c> link is structurally perfect.
///
/// No database is needed: the policy is pure given its settings.
/// </summary>
public class MediaUrlPolicyTests
{
    private const string StorageHost = MediaPolicyStub.StorageHost;

    private static MediaUrlPolicy Policy(MediaSettings? settings = null, string? storageBaseUrl = $"https://{StorageHost}/images") =>
        MediaPolicyStub.Build(settings, storageBaseUrl);

    // ------------------------------------------------------------ structural

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("http://example.com/photo.jpg")]
    public void Refuses_non_https_schemes(string url)
    {
        var act = () => Policy(Any()).EnsureImageUrl(url, "Images");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(BusinessErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Refuses_a_relative_url()
    {
        var act = () => Policy(Any()).EnsureImageUrl("/uploads/photo.jpg", "Images");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*URL absolue*");
    }

    [Fact]
    public void Refuses_credentials_embedded_in_the_url()
    {
        // https://cdn.example.com@evil.test/x reads as cdn.example.com to a
        // human and resolves to evil.test in a browser.
        var act = () => Policy(Any()).EnsureImageUrl("https://user:pass@cdn.example.com/photo.jpg", "Images");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*identifiants*");
    }

    [Fact]
    public void Allows_loopback_http_for_local_development()
    {
        var act = () => Policy(Any()).EnsureImageUrl("http://localhost:5000/photo.jpg", "Images");

        act.Should().NotThrow();
    }

    [Fact]
    public void Treats_an_empty_value_as_clearing_the_field()
    {
        var policy = Policy();

        policy.Invoking(p => p.EnsureImageUrl(null, "Images")).Should().NotThrow();
        policy.Invoking(p => p.EnsureImageUrl("   ", "Images")).Should().NotThrow();
    }

    // ------------------------------------------------------------ host rules

    [Fact]
    public void Fails_closed_when_no_hosts_are_configured()
    {
        // The point of the whole class: an unconfigured deployment must NOT
        // accept arbitrary URLs just because nobody filled in a list.
        var act = () => Policy().EnsureImageUrl("https://evil.test/photo.jpg", "Images");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*evil.test*");
    }

    [Fact]
    public void Always_allows_its_own_storage_account_without_configuration()
    {
        var act = () => Policy().EnsureImageUrl($"https://{StorageHost}/images/photo.jpg", "Images");

        act.Should().NotThrow();
    }

    [Fact]
    public void Allows_a_host_on_the_configured_list()
    {
        var settings = new MediaSettings { AllowedImageHosts = { "cdn.partner.example" } };

        Policy(settings).Invoking(p => p.EnsureImageUrl("https://cdn.partner.example/x.jpg", "Images"))
            .Should().NotThrow();

        Policy(settings).Invoking(p => p.EnsureImageUrl("https://other.example/x.jpg", "Images"))
            .Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void A_leading_dot_covers_subdomains()
    {
        var settings = new MediaSettings { AllowedImageHosts = { ".partner.example" } };

        Policy(settings).Invoking(p => p.EnsureImageUrl("https://media.partner.example/x.jpg", "Images"))
            .Should().NotThrow();
    }

    [Fact]
    public void Each_kind_carries_its_own_list()
    {
        var settings = new MediaSettings { AllowedVideoHosts = { "www.youtube.com" } };
        var policy = Policy(settings);

        policy.Invoking(p => p.EnsureVideoUrl("https://www.youtube.com/watch?v=abc", "VideoLink"))
            .Should().NotThrow();

        // A video host is not implicitly an image host.
        policy.Invoking(p => p.EnsureImageUrl("https://www.youtube.com/thumb.jpg", "Images"))
            .Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Survives_an_unconfigured_storage_account()
    {
        // GetUrl throwing must not take validation down with it — it just means
        // no implicit host is contributed.
        var policy = Policy(Any(), storageBaseUrl: null);

        policy.Invoking(p => p.EnsureImageUrl("https://anywhere.example/x.jpg", "Images"))
            .Should().NotThrow();
    }

    // -------------------------------------------------------- existing data

    [Fact]
    public void Skips_urls_the_entity_already_holds()
    {
        // §9 — an edit form resends what it was given. A project whose catalogue
        // predates this policy must stay editable, or the policy silently locks
        // admins out of every legacy record.
        var stored = new[] { "https://legacy-placeholder.test/old.jpg" };

        Policy().Invoking(p => p.EnsureImageUrls(stored, "Images", stored))
            .Should().NotThrow();

        // But adding a new one alongside it is still refused.
        var withNew = stored.Append("https://evil.test/new.jpg");

        Policy().Invoking(p => p.EnsureImageUrls(withNew, "Images", stored))
            .Should().Throw<BusinessRuleException>()
            .WithMessage("*evil.test*");
    }

    /// <summary>Opens every kind, to isolate the structural checks from the host rules.</summary>
    private static MediaSettings Any() => new()
    {
        AllowedImageHosts = { MediaSettings.AnyHost },
        AllowedVideoHosts = { MediaSettings.AnyHost },
        Allowed3DHosts = { MediaSettings.AnyHost }
    };
}
