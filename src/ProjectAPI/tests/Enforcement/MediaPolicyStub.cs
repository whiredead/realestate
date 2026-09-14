using Microsoft.Extensions.Options;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Domain.Common.Interfaces;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Builds a <see cref="MediaUrlPolicy"/> for tests whose subject is something
/// else (a scope rule, a state transition) but whose handler now takes the
/// policy as a dependency.
///
/// Those tests get <see cref="Permissive"/>, so a media rule can never be the
/// reason they pass or fail — they would otherwise start asserting two things
/// at once, and a tightened host list would break tests that have nothing to do
/// with media. MediaUrlPolicyTests is where the policy itself is exercised.
/// </summary>
internal static class MediaPolicyStub
{
    public const string StorageHost = "stgtest.blob.core.windows.net";

    public static MediaUrlPolicy Permissive() => Build(new MediaSettings
    {
        AllowedImageHosts = { MediaSettings.AnyHost },
        AllowedVideoHosts = { MediaSettings.AnyHost },
        Allowed3DHosts = { MediaSettings.AnyHost }
    });

    public static MediaUrlPolicy Build(
        MediaSettings? settings = null,
        string? storageBaseUrl = $"https://{StorageHost}/images") =>
        new(Options.Create(settings ?? new MediaSettings()), new FakeBlobStorage(storageBaseUrl));

    /// <summary>
    /// Stands in for the real blob service. Only GetUrl is ever reached — the
    /// policy uses it to learn its own storage host and never transfers bytes,
    /// so every other member throwing documents that fact rather than hiding it
    /// behind a silent no-op.
    /// </summary>
    private sealed class FakeBlobStorage : IBlobStorageService
    {
        private readonly string? _baseUrl;
        public FakeBlobStorage(string? baseUrl) => _baseUrl = baseUrl;

        public IBlobContainer ForContainer(string? containerName = null) => new FakeContainer(_baseUrl);

        private sealed class FakeContainer : IBlobContainer
        {
            private readonly string? _baseUrl;
            public FakeContainer(string? baseUrl) => _baseUrl = baseUrl;

            public string GetUrl(string blobName) =>
                _baseUrl is null
                    ? throw new InvalidOperationException("Storage is not configured.")
                    : $"{_baseUrl}/{blobName}";

            public Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<(Stream? Content, string? ContentType, bool Exists)> DownloadAsync(string blobName, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
