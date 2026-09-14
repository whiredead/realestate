using FluentAssertions;
using ProjectAPI.Api.Application.Sales.AfterSales.Attachments;
using ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;
using ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Scope J — proves the SAV attachment paths cannot be fed a URL.
///
/// Both claim commands used to carry a list of client-supplied addresses that
/// were stored verbatim as the attachment's location: a caller could point a
/// claim's "evidence" at any URL on the internet, and the console rendered it
/// as a link. These are reflection tests on purpose — the fix is the ABSENCE of
/// a field, and the only durable way to state "this must not come back" is to
/// fail the build's test run if it does.
/// </summary>
public class ClaimAttachmentPolicyTests
{
    [Fact]
    public void Creating_a_claim_cannot_carry_attachment_urls()
    {
        var properties = typeof(CreateAfterSaleClaimCommand).GetProperties().Select(p => p.Name);

        properties.Should().NotContain(
            "Files",
            "attachments are uploaded as files to a private container, never accepted as URLs");
    }

    [Fact]
    public void Resolving_a_claim_cannot_carry_proof_urls()
    {
        var properties = typeof(UpdateClaimStatusCommand).GetProperties().Select(p => p.Name);

        properties.Should().NotContain(
            "Proofs",
            "proof of repair is uploaded as a file, never accepted as a URL");
    }

    [Fact]
    public void The_upload_command_takes_a_file_not_a_url()
    {
        var file = typeof(UploadClaimAttachmentCommand).GetProperty("File");

        file.Should().NotBeNull();
        file!.PropertyType.Name.Should().Be("IFormFile");

        typeof(UploadClaimAttachmentCommand).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain("Url");
    }

    [Fact]
    public void Attachments_have_their_own_private_container()
    {
        // Not "images" (public catalogue) and not "documents" (reservation
        // dossiers): claim evidence is private and belongs to one buyer's file.
        ClaimAttachmentStorage.ContainerName.Should().Be("claim-attachments");
        ClaimAttachmentStorage.ContainerName.Should().NotBe("images");
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("application/pdf", true)]
    [InlineData("video/mp4", true)]
    [InlineData("text/html", false)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("application/zip", false)]
    [InlineData("image/svg+xml", false)]
    public void Only_evidence_media_types_are_accepted(string contentType, bool allowed)
    {
        // SVG is excluded deliberately despite being an image: it can carry
        // script, and these files are served back to other users.
        ClaimAttachmentStorage.AllowedContentTypes
            .Contains(contentType, StringComparer.OrdinalIgnoreCase)
            .Should().Be(allowed);
    }

    [Fact]
    public void The_size_ceiling_is_a_phone_photo_not_a_video_library()
    {
        ClaimAttachmentStorage.MaxSizeBytes.Should().Be(25 * 1024 * 1024);
    }
}
