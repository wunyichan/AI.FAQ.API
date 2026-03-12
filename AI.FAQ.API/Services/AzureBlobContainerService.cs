using AI.FAQ.API.DataModel;
using Azure.Storage.Blobs;

namespace AI.FAQ.API.Services
{
    public class AzureBlobContainerService
    {
        private readonly BlobServiceClient _blobService;

        public AzureBlobContainerService(string? blobStorageConnectionString)
        {
            if (string.IsNullOrWhiteSpace(blobStorageConnectionString))
            {
                throw new ArgumentException("Connection string cannot be null, empty, or whitespace.", nameof(blobStorageConnectionString));
            }

            _blobService = new BlobServiceClient(blobStorageConnectionString);
        }

        public BlobServiceClient GetBlobServiceClient()
        {
            return _blobService;
        }

        public async Task<BlobContainerClient> GetBlobContainerClient(string containerName)
        {
            var containerClient = _blobService.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            return containerClient;
        }

        public async Task<BooleanResult> UploadBlobAsync(string containerName, string newUploadFileName, IFormFile file)
        {
            var containerClient = await GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(newUploadFileName);
            try
            {
                using (var content = file.OpenReadStream())
                {
                    await blobClient.UploadAsync(content, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                return new BooleanResult(false, $"Failed to upload blob: {ex.Message}");
            }

            return new BooleanResult(true);
        }

        public async Task<BooleanResult> DownloadBlobAsync(string containerName, string blobName, Stream stream)
        {
            var containerClient = await GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            try
            {
                var existsResponse = await blobClient.ExistsAsync();
                if (!existsResponse.Value)
                {
                    return new BooleanResult(false, "Blob does not exist.");
                }

                var downloadResponse = await blobClient.DownloadToAsync(stream);
                stream.Position = 0;

                var success = downloadResponse.Status >= 200 && downloadResponse.Status <= 299;
                if (!success)
                {
                    return new BooleanResult(false, $"Failed to download blob, status code: {downloadResponse.Status}");
                }

                return new BooleanResult(true);
            }
            catch (Exception ex)
            {
                return new BooleanResult(false, $"Failed to download blob: {ex.Message}");
            }
        }

    }
}
