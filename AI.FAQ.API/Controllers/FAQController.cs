using AI.FAQ.API.DataModel;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using iText.Layout;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SkiaSharp;
using System.Text.Json;
using UglyToad.PdfPig.Rendering.Skia;

namespace AI.FAQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FAQController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        private readonly BlobServiceClient _blobService;
        private readonly DocumentIntelligenceClient _docClient;

        private const string QaPrompt = @"
                                            You are extracting Q&A pairs from a batch of PDF pages.
                                            Rules:
                                            1. Extract complete pairs into the 'pairs' array.
                                            2. For each pair, include 'page_range' (e.g., '3' or '3-4') indicating where the information appears.
                                            3. If the batch starts with text that completes a sentence from a previous page, put it in 'prefixCompletion'.
                                            4. If the batch ends with an unfinished question or answer, put that EXACT trailing text into 'suffixContext'.
                                            5. Return ONLY valid JSON:
                                            {
                                                ""prefixCompletion"": ""..."",
                                                ""pairs"": [ 
                                                {
                                                    ""question"": ""..."", 
                                                    ""answer"": ""..."", 
                                                    ""page_range"": ""3-4"",
                                                    ""figures"": [ 
                                                        {
                                                            ""caption"": ""..."",
                                                            ""page"": 4
                                                        }
                                                     ],
                                                     ""tables"": [
                                                        {
                                                            ""caption"": ""..."",
                                                            ""page"": 3
                                                } 
                                                ],
                                                ""suffixContext"": ""..."" 
                                            }";

        private readonly int batchSize = 5;

        public FAQController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
            _blobService = new BlobServiceClient(config["AzureBlobStorage:ConnectionString"]);
            _docClient = new DocumentIntelligenceClient(
                new Uri(config["DocumentIntelligence:Endpoint"] ?? ""),
                new AzureKeyCredential(config["DocumentIntelligence:Key"] ?? "")
                );
        }


        [HttpPost("1/upload-and-split")]
        public async Task<IActionResult> UploadAndSplit(IFormFile file)
        {
            string _uploadsContainerName = _config["AzureBlobStorage:UploadContainerName"] ?? "pdfuploads";
            string _splitsContainerName = _config["AzureBlobStorage:SplitContainerName"] ?? "pdfsplits";

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // 2. File size check (20 MB max)
            const long maxSize = 10 * 1024 * 1024; // 20 MB
            if (file.Length > maxSize) return BadRequest("File size exceeds 10 MB limit.");

            // 3. File extension check
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".pdf") return BadRequest("Only PDF files are allowed.");

            // 4. MIME type check
            if (file.ContentType != "application/pdf")
                return BadRequest("Invalid file type. Only PDF files are allowed.");


            //var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var newFileName = GenerateNewFileName();
            var fileName = Path.GetFileNameWithoutExtension(newFileName);

            var uploadsContainer = _blobService.GetBlobContainerClient(_uploadsContainerName);
            var splitsContainer = _blobService.GetBlobContainerClient(_splitsContainerName);

            await uploadsContainer.CreateIfNotExistsAsync();
            await splitsContainer.CreateIfNotExistsAsync();

            // 1. Upload original PDF to pdfuploads
            //var uploadBlob = uploadsContainer.GetBlobClient(file.FileName);

            var uploadBlob = uploadsContainer.GetBlobClient(newFileName);
            using (var uploadStream = file.OpenReadStream())
            {
                await uploadBlob.UploadAsync(uploadStream, overwrite: true);
            }
            // 2. Load PDF into memory
            PdfDocument inputPdf;
            using var ms = new MemoryStream();
            await uploadBlob.DownloadToAsync(ms); ms.Position = 0;
            inputPdf = PdfReader.Open(ms, PdfDocumentOpenMode.Import);

            // 3. Split pages and upload to pdfsplits/{filename}/
            for (int i = 0; i < inputPdf.PageCount; i++)
            {
                var outputPdf = new PdfDocument(); outputPdf.AddPage(inputPdf.Pages[i]);
                using var outStream = new MemoryStream();
                outputPdf.Save(outStream);
                outStream.Position = 0;
                var pageBlob = splitsContainer.GetBlobClient($"{fileName}/page-{i + 1}.pdf");
                await pageBlob.UploadAsync(outStream, overwrite: true);

                outputPdf.Dispose();
            }

            inputPdf.Dispose();
            ms.Dispose();

            return Ok(new
            {
                message = "PDF uploaded and split successfully.",
                original = uploadBlob.Uri.ToString(),
                splitFolder = $"pdfsplits/{fileName}/"
            });
        }

        private static string GenerateNewFileName()
        {
            string date = DateTime.Now.ToString("yyyyMMdd"); // Malaysia local time
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random();

            string randomPart = new string(
                Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)])
                .ToArray()
            );

            return $"{date}_{randomPart}.pdf";
        }

        [HttpGet("2/send-to-document-intelligent")]
        public async Task<IActionResult> SendToDocumentIntelligence()
        {
            var container = GetBlobServiceClientSafe(_blobService, _config["AzureBlobStorage:SplitContainerName"] ?? "pdfsplits");
            var results = new List<object>();

            await foreach (var folder in container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, "/", prefix: null, cancellationToken: HttpContext.RequestAborted))
            {
                if (!folder.IsPrefix) continue;
                string folderName = folder.Prefix.TrimEnd('/');
                var pages = new List<(int PageNumber, string Text)>();

                await foreach (var blobItem in container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, "/", prefix: folder.Prefix, cancellationToken: HttpContext.RequestAborted))
                {
                    if (blobItem.IsPrefix) continue;
                    string blobName = blobItem.Blob.Name;

                    var fileName = Path.GetFileNameWithoutExtension(blobName);
                    var parts = fileName.Split('-');


                    if (parts.Length == 2 && int.TryParse(parts[1], out int pageNum))
                    {
                        var blobClient = container.GetBlobClient(blobName);

                        #region Generate SAS URL (valid 30 min) - OPTIONAL: You can use this SAS URL approach if you want Document Intelligence to fetch the blob directly, instead of downloading it into memory and sending bytes.

                        //var sasBuilder = new BlobSasBuilder
                        //{
                        //    BlobContainerName = container.Name,
                        //    BlobName = blobName,
                        //    Resource = "b",
                        //    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
                        //};

                        //sasBuilder.SetPermissions(BlobSasPermissions.Read);

                        //var sasUri = blobClient.GenerateSasUri(sasBuilder);

                        #endregion

                        // Download blob into a MemoryStream so we can send it to Document Intelligence
                        using var ms = new MemoryStream();
                        await blobClient.DownloadToAsync(ms, cancellationToken: HttpContext.RequestAborted);
                        //what is the purpose of setting the position to 0 after downloading the blob into the MemoryStream?
                        ms.Position = 0;

                        // The SDK's AnalyzeDocument APIs accept the document bytes directly.
                        var document = BinaryData.FromStream(ms);

                        var options = new AnalyzeDocumentOptions("prebuilt-layout", document)
                        {
                            OutputContentFormat = DocumentContentFormat.Markdown
                        };

                        var result = await _docClient.AnalyzeDocumentAsync(
                            WaitUntil.Completed,
                            options,
                            cancellationToken: HttpContext.RequestAborted
                        );

                        ms.Dispose();

                        #region Save the result as JSON

                        var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
                        string finalJson = JsonSerializer.Serialize(result, serializerOptions);

                        string filePath = Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "",
                            folderName, "diResult_" + pageNum + ".json");

                        var directory = Path.GetDirectoryName(filePath) ?? _env.ContentRootPath;
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // 4. Write the file to disk
                        await System.IO.File.WriteAllTextAsync(filePath, finalJson);

                        #endregion

                        // --- AFTER SAVING JSON ---

                        // Define your target directory for images
                        string imageDir = Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "", folderName + "_images", $"page-{pageNum}");

                        if (result.Value.Tables.Count > 0 || result.Value.Figures.Count > 0)
                        {
                            Directory.CreateDirectory(imageDir);

                            using var pageImage = RenderPdfToImage(ms);

                            foreach (var page in result.Value.Pages)
                            {
                                float pageW = page.Width ?? 0.0f;
                                float pageH = page.Height ?? 0.0f;

                                float scaleX = pageImage.Width / pageW;
                                float scaleY = pageImage.Height / pageH;

                                // ---- TABLES ----
                                int tIndex = 1;
                                foreach (var table in result.Value.Tables)
                                {
                                    var region = table.BoundingRegions.First();
                                    CropAndSave(pageImage, region.Polygon, scaleX, scaleY,
                                        Path.Combine(imageDir, $"table_{tIndex}.png"));
                                    tIndex++;
                                }

                                // ---- FIGURES ----
                                int fIndex = 1;
                                foreach (var fig in result.Value.Figures)
                                {
                                    var region = fig.BoundingRegions.First();
                                    CropAndSave(pageImage, region.Polygon, scaleX, scaleY,
                                        Path.Combine(imageDir, $"figure_{fIndex}.png"));
                                    fIndex++;
                                }
                            }

                        }

                        string extractedText = result.Value.Content ?? string.Empty;
                        pages.Add((pageNum, extractedText));
                    }
                }

                // Sort pages by page number
                pages = pages.OrderBy(p => p.PageNumber).ToList();

                return Ok(new
                {
                    Folder = folder,
                    Pages = pages.Select(p => new
                    {
                        Page = p.PageNumber,
                        ExtractedText = p.Text
                    })
                });
            }

            return Ok(results);
        }

        // ---------------------------
        // Helper: Convert polygon → crop → save
        // ---------------------------
        private static void CropAndSave(SKBitmap pageBmp, IReadOnlyList<float> polygon,
            float scaleX, float scaleY, string outPath)
        {
            var (x, y, w, h) = PolygonToRect(polygon);

            var rect = new SKRectI(
                (int)(x * scaleX),
                (int)(y * scaleY),
                (int)((x + w) * scaleX),
                (int)((y + h) * scaleY)
            );

            using var subset = new SKBitmap(rect.Width, rect.Height);
            pageBmp.ExtractSubset(subset, rect);

            using var img = SKImage.FromBitmap(subset);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = System.IO.File.OpenWrite(outPath);
            data.SaveTo(fs);
        }

        private static (float x, float y, float w, float h) PolygonToRect(IReadOnlyList<float> p)
        {
            var xs = new List<float>();
            var ys = new List<float>();

            for (int i = 0; i < p.Count; i += 2)
            {
                xs.Add(p[i]);
                ys.Add(p[i + 1]);
            }

            float minX = xs.Min();
            float maxX = xs.Max();
            float minY = ys.Min();
            float maxY = ys.Max();

            return (minX, minY, maxX - minX, maxY - minY);
        }

        private SkiaSharp.SKBitmap RenderPdfToImage(Stream pdfStream)
        {
            pdfStream.Position = 0; // Ensure stream is at the beginning
            if (!pdfStream.CanSeek)
            {
                var ms = new MemoryStream();
                pdfStream.CopyTo(ms);
                ms.Position = 0;
                pdfStream = ms;
            }

            using var document = UglyToad.PdfPig.PdfDocument.Open(
                pdfStream,
                SkiaRenderingParsingOptions.Instance
            );

            document.AddSkiaPageFactory();

            int pageNumber = 1;
            float scale = 2.0f;

            // Use the overload your version supports
            using var bitmap = document.GetPageAsSKBitmap(pageNumber, scale);

            return bitmap.Copy();
        }


        [HttpGet("3/read")]
        public async Task<IActionResult> ConcatenatePages([FromQuery] string? targetFile)
        {
            targetFile = targetFile ?? _config["DIFolder:CurrentTarget"] ?? "";
            string directoryPath = Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "", targetFile);
            List<object> figureTablePages = new List<object>();
            List<object> allPages = new List<object>();

            if (Directory.Exists(directoryPath))
            {
                // 3. Get all files ending in .json
                string[] _jsonFiles = Directory.GetFiles(directoryPath, "*.json");
                List<string> jsonFiles = _jsonFiles
                    .OrderBy(filePath =>
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        // Split by underscore and parse the part after it as an int
                        string[] parts = fileName.Split('_');
                        if (parts.Length > 1 && int.TryParse(parts[1], out int pageNumber))
                        {
                            return pageNumber;
                        }
                        return 0; // Default if parsing fails
                    })
                    .ToList();

                foreach (string filePath in jsonFiles)
                {
                    string jsonContent = await System.IO.File.ReadAllTextAsync(filePath);

                    Page myPage = Page.FromJson(jsonContent);

                    int pageNum = ExtractPageNumber(filePath);

                    if (myPage?.Value?.Figures?.Length > 0 || myPage?.Value?.Tables?.Length > 0)
                    {
                        figureTablePages.Add(new
                        {
                            fileName = Path.GetFileNameWithoutExtension(filePath),
                            content = myPage?.Value?.Content,
                            figures = myPage?.Value?.Figures?.Length,
                            tables = myPage?.Value?.Tables?.Length
                        });
                    }

                    List<object> figures = new List<object>();
                    List<object> tables = new List<object>();

                    if (myPage?.Value?.Figures?.Length > 0)
                    {
                        string imageDirectoryPath = Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "", targetFile + "_images", $"page-{pageNum}");
                        if (Directory.Exists(imageDirectoryPath))
                        {
                            string[] _imgFiles = Directory.GetFiles(imageDirectoryPath, $"figure_*.png");

                            foreach (string imgPath in _imgFiles)
                            {
                                try
                                {
                                    // 1. Read file to binary
                                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                                    // 2. Convert binary to Base64 string
                                    string base64String = Convert.ToBase64String(fileBytes);

                                    figures.Add(new
                                    {
                                        caption = Path.GetFileNameWithoutExtension(imgPath),
                                        path = imgPath,
                                        data_image = $"data:image/png;base64,{base64String}"
                                    });
                                }
                                catch (IOException ex)
                                {
                                }
                            }
                        }
                    }

                    if (myPage?.Value?.Tables?.Length > 0)
                    {
                        string imageDirectoryPath = Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "", targetFile + "_images", $"page-{pageNum}");
                        if (Directory.Exists(imageDirectoryPath))
                        {
                            string[] _tableFiles = Directory.GetFiles(imageDirectoryPath, $"table_*.png");

                            foreach (string tablePath in _tableFiles)
                            {
                                try
                                {
                                    // 1. Read file to binary
                                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                                    // 2. Convert binary to Base64 string
                                    string base64String = Convert.ToBase64String(fileBytes);

                                    tables.Add(new
                                    {
                                        caption = Path.GetFileNameWithoutExtension(tablePath),
                                        path = tablePath,
                                        data_image = $"data:image/png;base64,{base64String}"
                                    });
                                }
                                catch (IOException ex)
                                {
                                }
                            }
                        }
                    }

                    allPages.Add(new
                    {
                        fileName = Path.GetFileNameWithoutExtension(filePath),
                        pageNo = ExtractPageNumber(filePath),
                        content = myPage?.Value?.Content,
                        figure_count = myPage?.Value?.Figures?.Length,
                        table_count = myPage?.Value?.Tables?.Length,
                        figures,
                        tables
                    });

                }


            }


            // 1. Serialize the final list to a pretty-printed JSON string
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
            string figureTableJson = JsonSerializer.Serialize(figureTablePages, serializerOptions);
            string allPagesJson = JsonSerializer.Serialize(allPages, serializerOptions);

            // 2. Define the path
            string figureTableFilePath = Path.Combine(_env.ContentRootPath, "Data", "pages_with_figures_tables.json");
            string allPageFilePath = Path.Combine(_env.ContentRootPath, "Data", "all_pages_data.json");

            // 3. Ensure the directory exists just in case
            // Avoid passing a possible null to Directory.CreateDirectory by using a fallback
            var figureTableDirectory = Path.GetDirectoryName(figureTableFilePath) ?? _env.ContentRootPath;
            if (!string.IsNullOrEmpty(figureTableDirectory))
            {
                Directory.CreateDirectory(figureTableDirectory);
            }

            var allPageDirectory = Path.GetDirectoryName(allPageFilePath) ?? _env.ContentRootPath;
            if (!string.IsNullOrEmpty(allPageDirectory))
            {
                Directory.CreateDirectory(allPageDirectory);
            }

            // 4. Write the file to disk
            await System.IO.File.WriteAllTextAsync(figureTableFilePath, figureTableJson);
            await System.IO.File.WriteAllTextAsync(allPageFilePath, allPagesJson);

            return Ok();
        }

        private int ExtractPageNumber(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string[] parts = fileName.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[1], out int pageNumber))
            {
                return pageNumber;
            }
            return 0; // Default if parsing fails
        }

        [HttpGet("3.5/test-read")]
        public async Task<IActionResult> TestRead()
        {
            // Load data.json from /Data folder
            string jsonPath = Path.Combine(_env.ContentRootPath, "Data", "all_pages_data.json");

            if (!System.IO.File.Exists(jsonPath))
                return NotFound("all_pages_data.json not found.");
            string json = await System.IO.File.ReadAllTextAsync(jsonPath);

            // Deserialize into model
            var data = JsonSerializer.Deserialize<AllPageInfo[]>(json);
            if (data == null || data.Length == 0)
                return BadRequest("No pages found.");

            return Ok(data.OrderBy(p => p.PageNo).ToList());
        }

        [HttpGet("4/open-ai-generate-qa")]
        public async Task<IActionResult> GenerateQA()
        {
            // Load data.json from /Data folder
            string jsonPath = Path.Combine(_env.ContentRootPath, "Data", "all_pages_data.json");

            if (!System.IO.File.Exists(jsonPath))
                return NotFound("all_pages_data.json not found.");
            string json = await System.IO.File.ReadAllTextAsync(jsonPath);

            // Deserialize into model
            var data = JsonSerializer.Deserialize<AllPageInfo[]>(json);
            if (data == null || data.Length == 0)
                return BadRequest("No pages found.");

            // 1. Sort and prepare pages
            var orderedPages = data.OrderBy(p => p.PageNo).ToList();
            string? leftoverContext = "";
            List<dynamic> qaPairs = new List<dynamic>();

            // Use the new client structure
            AzureOpenAIClient client = new AzureOpenAIClient(
                new Uri(_config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(_config["AzureOpenAI:Key"]!));


            // 1. Get a specific Chat Client for your deployment
            ChatClient chatClient = client.GetChatClient(_config["AzureOpenAI:Deployment"]!);

            // 2. Loop through pages in increments of 5
            for (int i = 0; i < orderedPages.Count; i += batchSize)
            {
                var batch = orderedPages.Skip(i).Take(batchSize);
                string currentBatchText = string.Join("\n\n", batch.Select(p => p.Content));

                // Prepend any context that was cut off from the PREVIOUS batch
                string promptInput = string.IsNullOrEmpty(leftoverContext)
                    ? currentBatchText
                    : $"[CONTINUATION FROM PREVIOUS PAGE]: {leftoverContext}\n\n{currentBatchText}";

                var messages = new List<ChatMessage> {
                                        new SystemChatMessage(QaPrompt),
                                        new UserChatMessage(promptInput)
                                    };

                // 3. Set options using ChatCompletionOptions
                ChatCompletionOptions options = new ChatCompletionOptions()
                {
                    Temperature = 0.1f
                };

                var response = await chatClient.CompleteChatAsync(messages, options);
                string resultJson = response.Value.Content?[0].Text.ToString() ?? "{}";

                // 3. Parse and handle continuity
                var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

                // Add complete pairs to our master list
                if (result.TryGetProperty("pairs", out var pairs))
                {
                    foreach (var pair in pairs.EnumerateArray())
                    {
                        qaPairs.Add(pair);
                    }
                }

                // Capture the 'suffixContext' to pass into the NEXT loop iteration
                leftoverContext = result.TryGetProperty("suffixContext", out var suffix)
                    ? suffix.GetString()
                    : "";
            }

            // 1. Serialize the final list to a pretty-printed JSON string
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
            string finalJson = JsonSerializer.Serialize(qaPairs, serializerOptions);

            // 2. Define the path (pointing to Data/qaPairs.json)
            string filePath = Path.Combine(_env.ContentRootPath, "Data", "qa-pairs.json");

            // 3. Ensure the directory exists just in case
            // Avoid passing a possible null to Directory.CreateDirectory by using a fallback
            var directory = Path.GetDirectoryName(filePath) ?? _env.ContentRootPath;
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 4. Write the file to disk
            await System.IO.File.WriteAllTextAsync(filePath, finalJson);

            return Ok(qaPairs);
        }

        // small helper to handle possible null container name scenarios in a single place
        private BlobContainerClient GetBlobServiceClientSafe(BlobServiceClient service, string containerName)
        {
            return service.GetBlobContainerClient(containerName);
        }
    }
}
