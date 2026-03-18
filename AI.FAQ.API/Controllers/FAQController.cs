using AI.FAQ.API.DataModel;
using AI.FAQ.API.Services;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI.FAQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FAQController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        private readonly AzureBlobContainerService azureBlobContainerService;
        private readonly AzureDocumentIntelligenceService azureDocumentIntelligenceService;
        private readonly AzureOpenAIClientService azureOpenAIClientService;

        private readonly int batchSize = 2;

        public FAQController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;

            azureBlobContainerService = new AzureBlobContainerService(
                ConfigService.GetConfigValue(config, "AzureBlobStorage:ConnectionString"));

            azureDocumentIntelligenceService = new AzureDocumentIntelligenceService(
                ConfigService.GetConfigValue(config, "DocumentIntelligence:Endpoint"),
                ConfigService.GetConfigValue(config, "DocumentIntelligence:Key"));

            azureOpenAIClientService = new AzureOpenAIClientService(
                ConfigService.GetConfigValue(config, "AzureOpenAI:Endpoint"),
                ConfigService.GetConfigValue(config, "AzureOpenAI:Key"));
        }

        [HttpPost("1/upload-and-split")]
        public async Task<IActionResult> UploadAndSplit(IFormFile file)
        {
            #region Check File Requirements

            var (check, checkError) = FileService.CheckFileRequirement(file, 20, new string[] { ".pdf" });
            if (!check)
            {
                return BadRequest(checkError);
            }

            #endregion

            string _uploadsContainerName = ConfigService.GetConfigValue(_config, "AzureBlobStorage:UploadContainerName", "pdfuploads");
            string _splitsContainerName = ConfigService.GetConfigValue(_config, "AzureBlobStorage:SplitContainerName", "pdfsplits");

            #region Upload the original PDF to the uploads container (keeps a copy of the original)

            string newFileName = FileService.GenerateNewFileName();
            var (uploadPDF, uploadPDFError) = await azureBlobContainerService.UploadBlobAsync(_uploadsContainerName, newFileName, file: file);

            if (!uploadPDF)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, uploadPDFError);
            }

            #endregion

            #region Download the PDF back from the uploads container and split into individual pages, saving each page as a separate PDF in the splits container

            using var ms = new MemoryStream();
            var (download, downloadError) = await azureBlobContainerService.DownloadBlobAsync(_uploadsContainerName, newFileName, ms);
            if (!download)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, downloadError);
            }

            PdfDocument inputPdf = PdfReader.Open(ms, PdfDocumentOpenMode.Import);

            var fileName = Path.GetFileNameWithoutExtension(newFileName);
            for (int ctr = 0; ctr < inputPdf.PageCount; ctr++)
            {
                var outputPdf = new PdfDocument();
                outputPdf.AddPage(inputPdf.Pages[ctr]);

                using var outStream = new MemoryStream();
                outputPdf.Save(outStream);
                outStream.Position = 0;

                var (uploadPage, uploadPageError) = await azureBlobContainerService.UploadBlobAsync(_splitsContainerName, $"{fileName}/page-{ctr + 1}.pdf", stream: outStream);
                if (!uploadPage)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to upload page {ctr + 1}: {uploadPageError}");
                }
            }

            #endregion

            return Ok(new
            {
                message = "PDF uploaded and split successfully."
            });
        }

        [HttpGet("2/send-to-document-intelligent")]
        public async Task<IActionResult> SendToDocumentIntelligence(string? targetFile)
        {
            targetFile = targetFile ?? ConfigService.GetConfigValue(_config, "DIFolder:CurrentTarget");

            if (!targetFile.EndsWith("/"))
                targetFile += "/";

            string folderName = targetFile.TrimEnd('/');

            string containerName = ConfigService.GetConfigValue(_config, "AzureBlobStorage:SplitContainerName", "pdfsplits");
            string fileDirectory = Path.Combine(_env.ContentRootPath, "Data", ConfigService.GetConfigValue(_config, "DIFolder:FolderName"));

            var results = new List<object>();
            var pages = new List<(int PageNumber, string Text)>();

            await foreach (var blobItem in azureBlobContainerService.GetContainerFolderBlobItemsAsync(containerName, targetFile, HttpContext.RequestAborted))
            {
                if (blobItem.IsPrefix) continue;
                string blobName = blobItem.Blob.Name;

                var fileName = Path.GetFileNameWithoutExtension(blobName);
                var parts = fileName.Split('-');

                if (parts.Length == 2 && int.TryParse(parts[1], out int pageNum))
                {
                    using var ms = new MemoryStream();
                    var (download, downloadError) = await azureBlobContainerService.DownloadBlobAsync(containerName, blobName, ms);
                    if (!download)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to download blob {blobName}: {downloadError}");
                    }

                    var result = await azureDocumentIntelligenceService.AnalyzePDFDocumentAsync(ms, DocumentModel.Layout, cancellationToken: HttpContext.RequestAborted);

                    var (valid, error) = await FileService.SaveAsJSONFile(Path.Combine(fileDirectory, folderName), $"diResult_{pageNum}", result);

                    if (!valid)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to save JSON for page {pageNum}: {error}");
                    }

                    if (result.Value.Tables.Count > 0 || result.Value.Figures.Count > 0)
                    {
                        string imageDir = Path.Combine(fileDirectory, folderName + "_images", $"page-{pageNum}");
                        Directory.CreateDirectory(imageDir);

                        using var pageImage = CropPDFImageService.RenderPdfToImage(ms);

                        foreach (var page in result.Value.Pages)
                        {
                            var (scaleX, scaleY) = CropPDFImageService.CalculateScaleFactors(pageImage, page);

                            // ---- TABLES ----
                            int tIndex = 1;
                            foreach (var table in result.Value.Tables)
                            {
                                var region = table.BoundingRegions.First();
                                CropPDFImageService.CropAndSave(pageImage, region.Polygon, scaleX, scaleY,
                                    Path.Combine(imageDir, $"table_{tIndex}.png"));
                                tIndex++;
                            }

                            // ---- FIGURES ----
                            int fIndex = 1;
                            foreach (var fig in result.Value.Figures)
                            {
                                var region = fig.BoundingRegions.First();
                                CropPDFImageService.CropAndSave(pageImage, region.Polygon, scaleX, scaleY,
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
                Folder = folderName,
                Pages = pages.Select(p => new
                {
                    Page = p.PageNumber,
                    ExtractedText = p.Text
                })
            });
        }

        [HttpGet("3/read")]
        public async Task<IActionResult> ConcatenatePages([FromQuery] string? targetFile)
        {
            targetFile = targetFile ?? ConfigService.GetConfigValue(_config, "DIFolder:CurrentTarget");
            string directoryPath = Path.Combine(_env.ContentRootPath, "Data",
                ConfigService.GetConfigValue(_config, "DIFolder:FolderName"), targetFile);
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
                    string jsonContent = await FileService.GetFileJSONContent(filePath);

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
                                figures.Add(new
                                {
                                    caption = Path.GetFileNameWithoutExtension(imgPath),
                                    path = imgPath
                                });
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
                                tables.Add(new
                                {
                                    caption = Path.GetFileNameWithoutExtension(tablePath),
                                    path = tablePath,
                                });
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

            await FileService.SaveAsJSONFile(Path.Combine(_env.ContentRootPath, "Data"), "pages_with_figures_tables", figureTablePages);
            await FileService.SaveAsJSONFile(Path.Combine(_env.ContentRootPath, "Data"), "all_pages_data", allPages);

            return Ok("JSON files created. ");
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
            string jsonContent = await FileService.GetFileJSONContent(Path.Combine(_env.ContentRootPath, "Data", "all_pages_data.json"));

            var data = AllPageInfo.FromJsonArray(jsonContent);
            if (data == null || data.Length == 0)
                return BadRequest("No pages found.");

            List<string> imagePath = new List<string>();
            var orderedPages = data.OrderBy(p => p.PageNo).ToList();
            for (int i = 0; i < orderedPages.Count; i += batchSize)
            {
                var batch = orderedPages.Skip(i).Take(batchSize);
                foreach (var page in batch)
                {
                    if (page.FigureCount > 0)
                    {
                        foreach (var fig in page.Figures!)
                        {
                            if (!String.IsNullOrEmpty(fig.Path) && System.IO.File.Exists(fig.Path))
                            {
                                imagePath.Add(fig.Path);
                            }
                        }
                    }

                    if (page.TableCount > 0)
                    {
                        foreach (var table in page.Tables!)
                        {
                            if (!String.IsNullOrEmpty(table.Path) && System.IO.File.Exists(table.Path))
                            {
                                imagePath.Add(table.Path);
                            }
                        }
                    }
                }
            }

            return Ok(imagePath);
        }

        [HttpGet("4/open-ai-generate-qa")]
        public async Task<IActionResult> GenerateQA(string? targetFile)
        {
            targetFile = targetFile ?? ConfigService.GetConfigValue(_config, "DIFolder:CurrentTarget");

            string jsonContent = await FileService.GetFileJSONContent(Path.Combine(_env.ContentRootPath, "Data", "all_pages_data.json"));
            string qaPrompt = await FileService.ReadTextFile(Path.Combine(_env.ContentRootPath, "Config", "system_prompt.txt")) ?? "Generate question-answer pairs based on the following content. Return the result in JSON format with two properties: 'pairs' which is an array of question-answer pairs, and 'suffixContext' which is any text from the end of the content that was cut off and should be included at the beginning of the next batch. Here is the content: \n\n{content}";

            // Deserialize into model
            var data = AllPageInfo.FromJsonArray(jsonContent);
            if (data == null || data.Length == 0)
                return BadRequest("No pages found.");

            // 1. Sort and prepare pages
            var orderedPages = data.OrderBy(p => p.PageNo).ToList();
            string? leftoverContext = "";
            List<dynamic> qaPairs = new List<dynamic>();

            ChatClient chatClient = azureOpenAIClientService.GetChatClient(ConfigService.GetConfigValue(_config, "AzureOpenAI:Deployment"));

            // 2. Loop through pages in increments 
            for (int i = 0; i < orderedPages.Count; i++)
            {
                var batch = new List<AllPageInfo> { orderedPages[i] };

                // LOOK-AHEAD INJECTION
                if (i + 1 < orderedPages.Count)
                {
                    var nextPage = orderedPages[i + 1];

                    if (ShouldLookAhead(leftoverContext, nextPage))
                    {
                        batch.Add(nextPage);
                        i++; // Skip next page because it's merged into this batch
                    }
                }
 ;
                string currentBatchText = string.Join("\n\n", batch.Select(p => p.Content));

                // Prepend any context that was cut off from the PREVIOUS batch
                string promptInput = string.IsNullOrEmpty(leftoverContext)
                    ? currentBatchText
                    : $"[CONTINUATION FROM PREVIOUS PAGE]: {leftoverContext}\n\n{currentBatchText}";

                var messages = new List<ChatMessage> {
                                        new SystemChatMessage(qaPrompt),
                                        new AssistantChatMessage(JsonSerializer.Serialize(qaPairs))
                                    };

                var userMessage = new UserChatMessage("");
                userMessage.Content.Add($"The total page for this PDF file is {orderedPages.Count}");
                userMessage.Content.Add(promptInput);


                foreach (var page in batch)
                {
                    if (page.FigureCount > 0)
                    {
                        foreach (var fig in page.Figures!)
                        {
                            if (!String.IsNullOrEmpty(fig.Path) && System.IO.File.Exists(fig.Path))
                            {
                                byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(fig.Path);
                                string imageTypeName = Path.GetExtension(Path.GetFileName(fig.Path)).Replace(".", String.Empty);

                                userMessage.Content.Add(fig.Path);
                                userMessage.Content.Add(
                                           ChatMessageContentPart.CreateImagePart(
                                               BinaryData.FromBytes(imageBytes),
                                               $"image/{imageTypeName}"
                                           )
                                       );
                            }
                        }
                    }

                    if (page.TableCount > 0)
                    {
                        foreach (var table in page.Tables!)
                        {
                            if (!String.IsNullOrEmpty(table.Path) && System.IO.File.Exists(table.Path))
                            {
                                byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(table.Path);
                                string imageTypeName = Path.GetExtension(Path.GetFileName(table.Path)).Replace(".", String.Empty);

                                userMessage.Content.Add(table.Path);
                                userMessage.Content.Add(
                                           ChatMessageContentPart.CreateImagePart(
                                               BinaryData.FromBytes(imageBytes),
                                               $"image/{imageTypeName}"
                                           )
                                       );
                            }
                        }
                    }

                    string diJSON = await FileService.GetFileJSONContent(Path.Combine(_env.ContentRootPath, "Data", _config["DIFolder:FolderName"] ?? "", targetFile, $"diResult_{page.PageNo}.json"));
                    userMessage.Content.Add($"\nResult from Azure Document Intelligence: {diJSON}\n");
                }

                messages.Add(userMessage);

                var response = await azureOpenAIClientService.GetChatCompletionAsync(chatClient, messages);
                string resultJson = response.Value.Content?[0].Text.ToString() ?? "{}";

                // 3. Parse and handle continuity
                var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

                if (result.ValueKind == JsonValueKind.Array)
                {
                    // Model returned raw array — treat it as full Q&A set
                    qaPairs = new List<dynamic>();
                    foreach (var pair in result.EnumerateArray())
                    {
                        qaPairs.Add(pair);
                    }
                    leftoverContext = "";
                }
                else if (result.ValueKind == JsonValueKind.Object)
                {
                    if (result.TryGetProperty("pairs", out var pairs))
                    {
                        var newPairs = new List<dynamic>();
                        foreach (var pair in pairs.EnumerateArray())
                        {
                            newPairs.Add(pair);
                        }
                        qaPairs = newPairs;
                    }

                    leftoverContext = result.TryGetProperty("suffixContext", out var suffix)
                        ? suffix.GetString()
                        : "";
                }
                else
                {
                    // Unexpected format
                    return BadRequest("Unexpected JSON format from model.");
                }
            }

            await FileService.SaveAsJSONFile(Path.Combine(_env.ContentRootPath, "Data"), "qaPairs", qaPairs);

            return Ok(new
            {
                total_questions = qaPairs.Count,
                qaPairs
            });
        }

        private bool ShouldLookAhead(string? leftoverContext, AllPageInfo nextPage)
        {
            // If previous batch ended mid‑answer, always merge
            if (!string.IsNullOrWhiteSpace(leftoverContext))
                return true;

            // A valid question must match: number + dot + text + question mark
            bool isNewQuestion = Regex.IsMatch(
                nextPage.Content.TrimStart(),
                @"^\d+\..*\?$",
                RegexOptions.Multiline
            );

            // If next page does NOT contain a valid question, it is continuation
            return !isNewQuestion;
        }
    }
}
