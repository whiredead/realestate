using Microsoft.Extensions.Options;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Common.Interfaces;

namespace ProjectAPI.Api.Application.Common.Media;

/// <summary>
/// Hosts the API will accept media links from, per kind.
///
/// An empty list is FAIL-CLOSED: only the API's own storage account is accepted
/// for that kind. Opening a kind to anywhere requires the explicit wildcard
/// entry <c>"*"</c> — a deployment cannot end up accepting arbitrary URLs by
/// forgetting to configure something.
/// </summary>
public class MediaSettings
{
    public const string SectionName = "Media";

    /// <summary>The one entry that disables host checking for a kind.</summary>
    public const string AnyHost = "*";

    public List<string> AllowedImageHosts { get; set; } = new();
    public List<string> AllowedVideoHosts { get; set; } = new();
    public List<string> Allowed3DHosts { get; set; } = new();
}

/// <summary>
/// Validates every media link the client supplies (§7.2 catalogue media).
///
/// A project image, a site video and a 3D-tour link are all rendered straight
/// into the public catalogue, so an unchecked string here is an injection point,
/// not a formatting nuisance: <c>javascript:</c> and <c>data:</c> URLs execute
/// in the visitor's page, and a plain <c>http:</c> link downgrades an https
/// page. The structural checks below refuse those outright.
///
/// On top of that, each kind carries a configurable host allow-list. The blob
/// account the API itself uploads to is ALWAYS allowed without being configured
/// — it is derived from the storage service rather than repeated in a settings
/// file, so rotating the account cannot silently lock the API out of its own
/// uploads.
/// </summary>
public class MediaUrlPolicy
{
    private readonly MediaSettings _settings;
    private readonly string? _ownStorageHost;

    public MediaUrlPolicy(IOptions<MediaSettings> settings, IBlobStorageService blobStorage)
    {
        _settings = settings.Value;

        // GetUrl does no round trip; it just formats the container's public URL.
        // A misconfigured or absent storage account must not stop validation
        // working, so anything unparseable simply contributes no extra host.
        try
        {
            var sample = blobStorage.ForContainer().GetUrl("probe");
            if (Uri.TryCreate(sample, UriKind.Absolute, out var uri))
            {
                _ownStorageHost = uri.Host;
            }
        }
        catch
        {
            _ownStorageHost = null;
        }
    }

    /// <summary>Project/quartier/type images. Throws 422 when refused.</summary>
    public void EnsureImageUrl(string? url, string field) =>
        Ensure(url, field, _settings.AllowedImageHosts, "image");

    /// <summary>Site-progress videos (§7.2 "espace temps réel").</summary>
    public void EnsureVideoUrl(string? url, string field) =>
        Ensure(url, field, _settings.AllowedVideoHosts, "vidéo");

    /// <summary>3D tour / virtual-visit links.</summary>
    public void Ensure3DLink(string? url, string field) =>
        Ensure(url, field, _settings.Allowed3DHosts, "visite 3D");

    /// <summary>
    /// Validates a whole image list, skipping any URL the entity already holds.
    ///
    /// An edit form resends the images it was given, so a project whose
    /// catalogue predates this policy (placeholder hosts, links typed in before
    /// the storage account existed) would become unsavable for an unrelated
    /// reason. Existing links are data and are preserved (§9); the policy
    /// applies to what the client is ADDING.
    /// </summary>
    public void EnsureImageUrls(IEnumerable<string>? urls, string field, IEnumerable<string>? alreadyStored = null)
    {
        if (urls is null) return;

        var known = alreadyStored is null
            ? null
            : new HashSet<string>(alreadyStored, StringComparer.OrdinalIgnoreCase);

        foreach (var url in urls)
        {
            if (known is not null && known.Contains(url?.Trim() ?? string.Empty)) continue;
            EnsureImageUrl(url, field);
        }
    }

    private void Ensure(string? url, string field, IReadOnlyCollection<string> allowedHosts, string kind)
    {
        // Empty is "not set" and is always allowed — clearing a link is legitimate.
        if (string.IsNullOrWhiteSpace(url)) return;

        var value = url.Trim();

        if (value.Length > 2048)
        {
            throw Refuse(field, $"Le lien {kind} dépasse 2048 caractères.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw Refuse(field, $"Le lien {kind} doit être une URL absolue.");
        }

        // Scheme check first: it is what rules out javascript:, data: and file:,
        // each of which parses perfectly well as an absolute URI.
        var isHttps = uri.Scheme == Uri.UriSchemeHttps;
        var isLocalHttp = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

        if (!isHttps && !isLocalHttp)
        {
            throw Refuse(field, $"Le lien {kind} doit utiliser https.");
        }

        // user:password@host — never legitimate here, and it hides the real host
        // from anyone eyeballing the link.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw Refuse(field, $"Le lien {kind} ne peut pas contenir d'identifiants.");
        }

        if (IsHostAllowed(uri.Host, allowedHosts)) return;

        throw Refuse(
            field,
            $"Le domaine « {uri.Host} » n'est pas autorisé pour les liens {kind}. " +
            "Téléversez le fichier ou demandez l'ajout du domaine à la configuration.");
    }

    private bool IsHostAllowed(string host, IReadOnlyCollection<string> allowedHosts)
    {
        if (_ownStorageHost is not null &&
            string.Equals(host, _ownStorageHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fail closed: an unconfigured kind accepts our own storage and nothing
        // else. Opening it up is an explicit "*", never an omission.
        if (allowedHosts.Contains(MediaSettings.AnyHost)) return true;

        return allowedHosts.Any(allowed =>
            string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase)
            // A leading dot means "this domain and its subdomains", so one entry
            // covers e.g. both cdn.example.com and media.example.com.
            || (allowed.StartsWith('.') && host.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)));
    }

    private static BusinessRuleException Refuse(string field, string message) =>
        new(BusinessErrorCodes.ValidationFailed, $"{field} : {message}");
}
