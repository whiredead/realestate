namespace ProjectAPI.Infrastructure.Settings;

/// <summary>
/// Azure Blob Storage connection, bound once from configuration (appsettings +
/// environment/user-secrets override in real deployments — see the doc
/// comment on ConnectionString) so the storage account and default container
/// can change without touching any handler or the DI registration below.
/// <see cref="IBlobStorageService.ForContainer"/> is what lets a caller
/// target a different container than <see cref="ContainerName"/> (e.g.
/// "documents" vs "images") without a second connection or a restart.
/// </summary>
public class BlobStorageSettings
{
    /// <summary>
    /// Standard Azure Storage connection string
    /// ("DefaultEndpointsProtocol=...;AccountName=...;AccountKey=...;EndpointSuffix=...").
    /// The whole account lives in this one value — rotating the key or
    /// pointing at a different storage account entirely is a single-line
    /// change here (BlobStorage:ConnectionString in appsettings, or the
    /// BlobStorage__ConnectionString env var), nothing else in the app knows
    /// or cares. Must come from environment/user-secrets in real
    /// deployments, never committed in appsettings.json.
    /// </summary>
    public string ConnectionString { get; set; } = default!;

    /// <summary>Default container used when a caller doesn't ask for a specific one.</summary>
    public string ContainerName { get; set; } = default!;
}
