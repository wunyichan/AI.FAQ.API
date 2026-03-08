using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace AI.FAQ.API.DataModel
{
    public partial class Page
    {
        [JsonProperty("Value")]
        public Value? Value { get; set; }

        [JsonProperty("HasValue")]
        public bool? HasValue { get; set; }

        [JsonProperty("Id")]
        public Guid? Id { get; set; }

        [JsonProperty("HasCompleted")]
        public bool? HasCompleted { get; set; }
    }

    public partial class Value
    {
        [JsonProperty("ApiVersion")]
        public DateTimeOffset? ApiVersion { get; set; }

        [JsonProperty("ModelId")]
        public string? ModelId { get; set; }

        [JsonProperty("ContentFormat")]
        public ContentFormat? ContentFormat { get; set; }

        [JsonProperty("Content")]
        public string? Content { get; set; }

        [JsonProperty("Pages")]
        public PageElement[]? Pages { get; set; }

        [JsonProperty("Paragraphs")]
        public Paragraph[]? Paragraphs { get; set; }

        [JsonProperty("Tables")]
        public Table[]? Tables { get; set; }

        [JsonProperty("Figures")]
        public Figure[]? Figures { get; set; }

        [JsonProperty("Sections")]
        public Section[]? Sections { get; set; }

        [JsonProperty("KeyValuePairs")]
        public object[]? KeyValuePairs { get; set; }

        [JsonProperty("Styles")]
        public object[]? Styles { get; set; }

        [JsonProperty("Languages")]
        public object[]? Languages { get; set; }

        [JsonProperty("Documents")]
        public object[]? Documents { get; set; }

        [JsonProperty("Warnings")]
        public object[]? Warnings { get; set; }
    }

    public partial class ContentFormat
    {
    }

    public partial class Figure
    {
        [JsonProperty("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }

        [JsonProperty("Elements")]
        public string[]? Elements { get; set; }

        [JsonProperty("Caption")]
        public object? Caption { get; set; }

        [JsonProperty("Footnotes")]
        public object[]? Footnotes { get; set; }

        [JsonProperty("Id")]
        public string? Id { get; set; }
    }

    public partial class BoundingRegion
    {
        [JsonProperty("PageNumber")]
        public long? PageNumber { get; set; }

        [JsonProperty("Polygon")]
        public double[]? Polygon { get; set; }
    }

    public partial class Span
    {
        [JsonProperty("Offset")]
        public long? Offset { get; set; }

        [JsonProperty("Length")]
        public long? Length { get; set; }
    }

    public partial class PageElement
    {
        [JsonProperty("PageNumber")]
        public long? PageNumber { get; set; }

        [JsonProperty("Angle")]
        public double? Angle { get; set; }

        [JsonProperty("Width")]
        public double? Width { get; set; }

        [JsonProperty("Height")]
        public long? Height { get; set; }

        [JsonProperty("Unit")]
        public ContentFormat? Unit { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }

        [JsonProperty("Words")]
        public SelectionMark[]? Words { get; set; }

        [JsonProperty("SelectionMarks")]
        public SelectionMark[]? SelectionMarks { get; set; }

        [JsonProperty("Lines")]
        public Line[]? Lines { get; set; }

        [JsonProperty("Barcodes")]
        public object[]? Barcodes { get; set; }

        [JsonProperty("Formulas")]
        public object[]? Formulas { get; set; }
    }

    public partial class Line
    {
        [JsonProperty("Content")]
        public string? Content { get; set; }

        [JsonProperty("Polygon")]
        public double[]? Polygon { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }
    }

    public partial class SelectionMark
    {
        [JsonProperty("State", NullValueHandling = NullValueHandling.Ignore)]
        public ContentFormat? State { get; set; }

        [JsonProperty("Polygon")]
        public double[]? Polygon { get; set; }

        [JsonProperty("Span")]
        public Span? Span { get; set; }

        [JsonProperty("Confidence")]
        public double? Confidence { get; set; }

        [JsonProperty("Content", NullValueHandling = NullValueHandling.Ignore)]
        public string? Content { get; set; }
    }

    public partial class Paragraph
    {
        [JsonProperty("Role")]
        public ContentFormat? Role { get; set; }

        [JsonProperty("Content")]
        public string? Content { get; set; }

        [JsonProperty("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }
    }

    public partial class Section
    {
        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }

        [JsonProperty("Elements")]
        public string[]? Elements { get; set; }
    }

    public partial class Table
    {
        [JsonProperty("RowCount")]
        public long? RowCount { get; set; }

        [JsonProperty("ColumnCount")]
        public long? ColumnCount { get; set; }

        [JsonProperty("Cells")]
        public Cell[]? Cells { get; set; }

        [JsonProperty("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }

        [JsonProperty("Caption")]
        public object? Caption { get; set; }

        [JsonProperty("Footnotes")]
        public object[]? Footnotes { get; set; }
    }

    public partial class Cell
    {
        [JsonProperty("Kind")]
        public ContentFormat? Kind { get; set; }

        [JsonProperty("RowIndex")]
        public long? RowIndex { get; set; }

        [JsonProperty("ColumnIndex")]
        public long? ColumnIndex { get; set; }

        [JsonProperty("RowSpan")]
        public long? RowSpan { get; set; }

        [JsonProperty("ColumnSpan")]
        public object? ColumnSpan { get; set; }

        [JsonProperty("Content")]
        public string? Content { get; set; }

        [JsonProperty("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonProperty("Spans")]
        public Span[]? Spans { get; set; }

        [JsonProperty("Elements")]
        public string[]? Elements { get; set; }
    }

    public partial class Page
    {
        public static Page FromJson(string? json)
        {
            // Deserialize and ensure non-null result; throw if deserialization produced null.
            return JsonConvert.DeserializeObject<Page>(json!, Converter.Settings)
                   ?? throw new JsonSerializationException("Failed to deserialize JSON into Page: result was null.");
        }
    }

    public static class Serialize
    {
        public static string? ToJson(this Page self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }


}
