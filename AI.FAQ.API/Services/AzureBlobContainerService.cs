using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Runtime.CompilerServices;

namespace AI.FAQ.API.Services
{
    public class AzureBlobContainerService
    {
        private readonly BlobServiceClient _blobService;

        public AzureBlobContainerService(string blobStorageConnectionString)
        {
            _blobService = new BlobServiceClient(blobStorageConnectionString);
        }

        public async Task<BlobContainerClient> GetBlobContainerClient(string containerName, bool checkCreateIfNotExists = false)
        {
            var containerClient = _blobService.GetBlobContainerClient(containerName);

            if (checkCreateIfNotExists)
                await containerClient.CreateIfNotExistsAsync();

            return containerClient;
        }

        public async Task<(bool, string)> UploadBlobAsync(string containerName, string newUploadBlobName, IFormFile? file = null, Stream? stream = null)
        {
            if (file == null && stream == null)
            {
                return new (false, "Either file or stream must be provided.");
            }

            var containerClient = await GetBlobContainerClient(containerName, true);
            var blobClient = containerClient.GetBlobClient(newUploadBlobName);
            try
            {
                if (stream != null)
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }
                else if (file != null)
                {
                    using (var content = file.OpenReadStream())
                    {
                        await blobClient.UploadAsync(content, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                return new (false, $"Failed to upload blob: {ex.Message}");
            }

            return new (true, "");
        }

        public async Task<(bool, string)> DownloadBlobAsync(string containerName, string blobName, Stream stream)
        {
            var containerClient = await GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            try
            {
                var existsResponse = await blobClient.ExistsAsync();
                if (!existsResponse.Value)
                {
                    return new (false, "Blob does not exist.");
                }

                var downloadResponse = await blobClient.DownloadToAsync(stream);
                stream.Position = 0;

                var success = downloadResponse.Status >= 200 && downloadResponse.Status <= 299;
                if (!success)
                {
                    return new (false, $"Failed to download blob, status code: {downloadResponse.Status}");
                }

                return new (true, "");
            }
            catch (Exception ex)
            {
                return new (false, $"Failed to download blob: {ex.Message}");
            }
        }

        public async IAsyncEnumerable<BlobHierarchyItem> GetContainerFolderBlobItemsAsync(string containerName, string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var containerClient = await GetBlobContainerClient(containerName);

            var resultSegments = containerClient.GetBlobsByHierarchyAsync(
                BlobTraits.None,
                BlobStates.None,
                "/",
                prefix: prefix,
                cancellationToken: cancellationToken);

            await foreach (var item in resultSegments)
            {
                yield return item;
            }
        }

        public async Task<Uri> GetSASUri(string containerName, string blobName, int timeInMinutes)
        {
            var containerClient = await GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerClient.Name,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(timeInMinutes)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);

            return sasUri;
        }
    }
}
