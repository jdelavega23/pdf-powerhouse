namespace PdfEngine.Core.Models;

public enum ImageFitMode
{
    FitPage,   // Ajusta la imagen dentro de los márgenes manteniendo la proporción
    FillPage,  // Ocupa toda la página
    OriginalSize
}

public record ImagesToPdfOptions
{
    public ImageFitMode FitMode { get; init; } = ImageFitMode.FitPage;
    public double MarginPt { get; init; } = 20;
    public bool AutoOrientation { get; init; } = true; // Vertical u Horizontal según la imagen
}
