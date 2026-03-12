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
    }

    public class FigureInfo
    {
        public string? caption { get; set; }
        public string? path { get; set; }
        public string? data_image { get; set; }
    }

    public class TableInfo
    {
        public string? caption { get; set; }
        public string? path { get; set; }
        public string? data_image { get; set; }
    }
}
