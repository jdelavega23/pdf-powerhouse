namespace PdfEngine.Core.Models;

public record PdfPageInfo
{
    public int PageIndex { get; init; }
    public int PageNumber => PageIndex + 1;
    public double Width { get; init; }
    public double Height { get; init; }
    public int Rotation { get; init; }
    public string Orientation => Width >= Height ? "Landscape" : "Portrait";
}
