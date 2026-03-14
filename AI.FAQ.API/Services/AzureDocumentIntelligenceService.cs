using Azure;
using Azure.AI.DocumentIntelligence;

namespace AI.FAQ.API.Services
{
    public enum DocumentModel
    {
        Layout, Document, Custom
    }

    public class AzureDocumentIntelligenceService
    {
        private readonly DocumentIntelligenceClient _docClient;

        public AzureDocumentIntelligenceService(string uri, string key)
        {
            _docClient = new DocumentIntelligenceClient(new Uri(uri), new AzureKeyCredential(key));
        }

        public async Task<Operation<AnalyzeResult>> AnalyzePDFDocumentAsync(Stream stream, DocumentModel modelId, CancellationToken cancellationToken = default, string? customModel = "")
        {
            stream.Position = 0;

            var document = BinaryData.FromStream(stream);

            var options = GetAnalyzeDocumentOptions(modelId, document, customModel);

            var result = await _docClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                options,
                cancellationToken: cancellationToken
            );

            return result;
        }

        private string GetModelId(DocumentModel model, string customModel = "")
        {
            if(model == DocumentModel.Custom && string.IsNullOrEmpty(customModel))
            {
                throw new ArgumentException("Custom model ID must be provided for Custom document model.");
            }

            return model switch
            {
                DocumentModel.Layout => "prebuilt-layout",
                DocumentModel.Document => "prebuilt-document",
                DocumentModel.Custom => customModel, // Replace with actual custom model ID
                _ => throw new ArgumentException("Invalid document model.")
            };
        }

        private AnalyzeDocumentOptions GetAnalyzeDocumentOptions(DocumentModel model, BinaryData document, string? customModel = "")
        {
            var options = model == DocumentModel.Layout ? new AnalyzeDocumentOptions("prebuilt-layout", document)
            {
                OutputContentFormat = DocumentContentFormat.Markdown
            } : new AnalyzeDocumentOptions(GetModelId(model, customModel ?? ""), document);

            return options;
        }

    }
}
