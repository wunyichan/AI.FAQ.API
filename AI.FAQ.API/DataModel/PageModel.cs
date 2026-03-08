using System.Text.Json.Serialization;

namespace AI.FAQ.API.DataModel
{
    public class AllPageInfo
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("pageNo")]
        public int PageNo { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        [JsonPropertyName("figures")]
        public int Figures { get; set; }
        [JsonPropertyName("tables")]  
        public int Tables { get; set; }
    }

    public class RootData
    {
        [JsonPropertyName("folder")]
        public FolderInfo? Folder { get; set; }

        [JsonPropertyName("pages")]
        public List<PageInfo>? Pages { get; set; }
    }

    public class FolderInfo
    {
        [JsonPropertyName("prefix")]
        public string? Prefix { get; set; }

        [JsonPropertyName("blob")]
        public object? Blob { get; set; }

        [JsonPropertyName("isPrefix")]
        public bool IsPrefix { get; set; }

        [JsonPropertyName("isBlob")]
        public bool IsBlob { get; set; }
    }

    public class PageInfo
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("extractedText")]
        public string? ExtractedText { get; set; }
    }
}
