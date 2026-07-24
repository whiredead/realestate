using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectAPI.Domain.Common.Abstractions
{

    public interface IBaseBlobStorage
    {
        Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default);
    }
}
