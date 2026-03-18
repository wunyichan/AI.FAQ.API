using System.Text.Json;
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

        [JsonPropertyName("figure_count")]
        public int FigureCount { get; set; }

        [JsonPropertyName("table_count")]
        public int TableCount { get; set; }

        [JsonPropertyName("figures")]
        public List<FigureInfo>? Figures { get; set; }

        [JsonPropertyName("tables")]
        public List<TableInfo>? Tables { get; set; }

        public static AllPageInfo FromJson(string json)
        {
            return JsonSerializer.Deserialize<AllPageInfo>(json) ?? throw new Exception("Failed to deserialize");
        }

        public static AllPageInfo[] FromJsonArray(string json)
        {
            return JsonSerializer.Deserialize<AllPageInfo[]>(json) ?? throw new Exception("Failed to deserialize");
        }
    }

    public class FigureInfo
    {
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }

    public class TableInfo
    {
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }
}
