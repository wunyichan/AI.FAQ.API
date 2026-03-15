using System.Text.Json;

namespace AI.FAQ.API.Services
{
    public static class FileService
    {
        private static readonly Dictionary<string, byte[]> _fileSignatures = new()
        {
            { ".docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
            { ".xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
            { ".pptx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
            { ".pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } },
            { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
            { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".json", new byte[] { 0x7B } },
            { ".zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
            { ".7z", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } }
        };

        public static string GenerateNewFileName()
        {
            string date = DateTime.Now.ToString("yyyyMMddhhmmss"); // Malaysia local time
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random();

            string randomPart = new string(
                Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)])
                .ToArray()
            );

            return $"{date}_{randomPart}.pdf";
        }

        public static (bool, string) CheckFileRequirement(IFormFile file, int sizeInMB, string[] allowedFileExtensions)
        {
            if (file == null || file.Length == 0) return new (false, "Invalid file.");
            if (file.Length > (long)sizeInMB * 1024 * 1024) return new (false, $"Too large. Max file size is {sizeInMB} MB. ");

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedFileExtensions.Contains(ext)) return new (false, "Invalid extension.");

            if (_fileSignatures.TryGetValue(ext, out byte[]? signature) && signature != null)
            {
                using (var reader = new BinaryReader(file.OpenReadStream()))
                {
                    var header = reader.ReadBytes(signature.Length);
                    if (!header.SequenceEqual(signature))
                    {
                        return new (false, "File content mismatch.");
                    }
                }
            }
            else
            {
                return new (false, "Unable to read file content. ");
            }

            return new (true, "");
        }

        public static async Task<string> GetFileJSONContent(string filePath)
        {
            try
            {
                if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    throw new Exception(filePath + " is not a JSON file. ");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("File Not Found. ");

                return await File.ReadAllTextAsync(filePath);
            }
            catch
            {
                throw new FileNotFoundException("File Not Found. ");
            }
        }

        public static async Task<(bool, string)> SaveAsJSONFile(string directoryPath, string fileName, object data)
        {
            try
            {
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    fileName = $"{fileName}.json";

                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                string finalJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(directoryPath, fileName), finalJson);

                return new (true, "");
            }
            catch (Exception ex)
            {
                return new (false, $"Error saving JSON file: {ex.Message}");
            }
        }
    }
}
