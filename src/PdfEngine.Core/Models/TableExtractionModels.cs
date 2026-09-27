namespace PdfEngine.Core.Models;

public sealed record TableExtractionOptions
{
    public string Delimiter { get; init; } = ",";
    public bool FirstRowIsHeader { get; init; } = true;
    public int? TargetPage { get; init; } = null;
    public double MinColumnGap { get; init; } = 15.0;
}

public sealed record PdfTableCell
{
    public int ColumnIndex { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed record PdfTableRow
{
    public int RowIndex { get; init; }
    public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();
}

public sealed record PdfTable
{
    public int PageNumber { get; init; }
    public int TableIndex { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PdfTableRow> Rows { get; init; } = Array.Empty<PdfTableRow>();
    public string Csv { get; init; } = string.Empty;
}

public sealed record TableExtractionResult
{
    public int TotalTables { get; init; }
    public int TotalRows { get; init; }
    public IReadOnlyList<PdfTable> Tables { get; init; } = Array.Empty<PdfTable>();
    public string CombinedCsv { get; init; } = string.Empty;
}
