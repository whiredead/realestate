using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ProjectAPI.Domain.Common.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectAPI.Infrastructure.Common
{
    public abstract class BaseBlobStorage : IBaseBlobStorage
    {
        protected readonly BlobContainerClient _containerClient;

        protected BaseBlobStorage(BlobContainerClient containerClient)
        {
            _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        }

        public virtual async Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
            return blobClient.Uri.ToString();
        }
    }
}
