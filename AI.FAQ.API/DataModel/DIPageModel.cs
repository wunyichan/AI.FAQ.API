using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace AI.FAQ.API.DataModel
{
    public partial class Page
    {
        [JsonPropertyName("Value")]
        public Value? Value { get; set; }

        [JsonPropertyName("HasValue")]
        public bool? HasValue { get; set; }

        [JsonPropertyName("Id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("HasCompleted")]
        public bool? HasCompleted { get; set; }
    }

    public partial class Value
    {
        [JsonPropertyName("ApiVersion")]
        public DateTimeOffset? ApiVersion { get; set; }

        [JsonPropertyName("ModelId")]
        public string? ModelId { get; set; }

        [JsonPropertyName("ContentFormat")]
        public ContentFormat? ContentFormat { get; set; }

        [JsonPropertyName("Content")]
        public string? Content { get; set; }

        [JsonPropertyName("Pages")]
        public PageElement[]? Pages { get; set; }

        [JsonPropertyName("Paragraphs")]
        public Paragraph[]? Paragraphs { get; set; }

        [JsonPropertyName("Tables")]
        public Table[]? Tables { get; set; }

        [JsonPropertyName("Figures")]
        public Figure[]? Figures { get; set; }

        [JsonPropertyName("Sections")]
        public Section[]? Sections { get; set; }

        [JsonPropertyName("KeyValuePairs")]
        public object[]? KeyValuePairs { get; set; }

        [JsonPropertyName("Styles")]
        public object[]? Styles { get; set; }

        [JsonPropertyName("Languages")]
        public object[]? Languages { get; set; }

        [JsonPropertyName("Documents")]
        public object[]? Documents { get; set; }

        [JsonPropertyName("Warnings")]
        public object[]? Warnings { get; set; }
    }

    public partial class ContentFormat
    {
    }

    public partial class Figure
    {
        [JsonPropertyName("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }

        [JsonPropertyName("Elements")]
        public string[]? Elements { get; set; }

        [JsonPropertyName("Caption")]
        public object? Caption { get; set; }

        [JsonPropertyName("Footnotes")]
        public object[]? Footnotes { get; set; }

        [JsonPropertyName("Id")]
        public string? Id { get; set; }
    }

    public partial class BoundingRegion
    {
        [JsonPropertyName("PageNumber")]
        public long? PageNumber { get; set; }

        [JsonPropertyName("Polygon")]
        public double[]? Polygon { get; set; }
    }

    public partial class Span
    {
        [JsonPropertyName("Offset")]
        public long? Offset { get; set; }

        [JsonPropertyName("Length")]
        public long? Length { get; set; }
    }

    public partial class PageElement
    {
        [JsonPropertyName("PageNumber")]
        public long? PageNumber { get; set; }

        [JsonPropertyName("Angle")]
        public double? Angle { get; set; }

        [JsonPropertyName("Width")]
        public double? Width { get; set; }

        [JsonPropertyName("Height")]
        public long? Height { get; set; }

        [JsonPropertyName("Unit")]
        public ContentFormat? Unit { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }

        [JsonPropertyName("Words")]
        public SelectionMark[]? Words { get; set; }

        [JsonPropertyName("SelectionMarks")]
        public SelectionMark[]? SelectionMarks { get; set; }

        [JsonPropertyName("Lines")]
        public Line[]? Lines { get; set; }

        [JsonPropertyName("Barcodes")]
        public object[]? Barcodes { get; set; }

        [JsonPropertyName("Formulas")]
        public object[]? Formulas { get; set; }
    }

    public partial class Line
    {
        [JsonPropertyName("Content")]
        public string? Content { get; set; }

        [JsonPropertyName("Polygon")]
        public double[]? Polygon { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }
    }

    public partial class SelectionMark
    {
        [JsonPropertyName("State")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ContentFormat? State { get; set; }

        [JsonPropertyName("Polygon")]
        public double[]? Polygon { get; set; }

        [JsonPropertyName("Span")]
        public Span? Span { get; set; }

        [JsonPropertyName("Confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("Content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }
    }

    public partial class Paragraph
    {
        [JsonPropertyName("Role")]
        public ContentFormat? Role { get; set; }

        [JsonPropertyName("Content")]
        public string? Content { get; set; }

        [JsonPropertyName("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }
    }

    public partial class Section
    {
        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }

        [JsonPropertyName("Elements")]
        public string[]? Elements { get; set; }
    }

    public partial class Table
    {
        [JsonPropertyName("RowCount")]
        public long? RowCount { get; set; }

        [JsonPropertyName("ColumnCount")]
        public long? ColumnCount { get; set; }

        [JsonPropertyName("Cells")]
        public Cell[]? Cells { get; set; }

        [JsonPropertyName("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }

        [JsonPropertyName("Caption")]
        public object? Caption { get; set; }

        [JsonPropertyName("Footnotes")]
        public object[]? Footnotes { get; set; }
    }

    public partial class Cell
    {
        [JsonPropertyName("Kind")]
        public ContentFormat? Kind { get; set; }

        [JsonPropertyName("RowIndex")]
        public long? RowIndex { get; set; }

        [JsonPropertyName("ColumnIndex")]
        public long? ColumnIndex { get; set; }

        [JsonPropertyName("RowSpan")]
        public long? RowSpan { get; set; }

        [JsonPropertyName("ColumnSpan")]
        public object? ColumnSpan { get; set; }

        [JsonPropertyName("Content")]
        public string? Content { get; set; }

        [JsonPropertyName("BoundingRegions")]
        public BoundingRegion[]? BoundingRegions { get; set; }

        [JsonPropertyName("Spans")]
        public Span[]? Spans { get; set; }

        [JsonPropertyName("Elements")]
        public string[]? Elements { get; set; }
    }

    public partial class Page
    {
        public static Page FromJson(string? json)
        {
            return JsonSerializer.Deserialize<Page>(json!) ?? throw new Exception("Failed to deserialize");
        }
    }

}
