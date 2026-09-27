namespace PdfEngine.Core.Models;

public enum PageNumberPosition
{
    BottomRight,
    BottomCenter,
    BottomLeft,
    TopRight,
    TopCenter,
    TopLeft
}

public record PageNumberOptions
{
    public string Format { get; init; } = "Página {n} de {total}"; // e.g. "Página {n} de {total}", "{n} / {total}", "{n}"
    public PageNumberPosition Position { get; init; } = PageNumberPosition.BottomRight;
    public int StartPage { get; init; } = 1; // Página 1-indexed donde empezar a numerar
    public int StartingNumber { get; init; } = 1; // Número visual inicial
    public double FontSize { get; init; } = 10;
    public string FontColorHex { get; init; } = "#555555";
    public double MarginPt { get; init; } = 30;
}
