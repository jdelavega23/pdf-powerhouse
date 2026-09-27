using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfOcrService
{
    /// <summary>
    /// Realiza OCR sobre un archivo de imagen (PNG, JPEG, TIFF) y extrae texto con métricas de confianza.
    /// </summary>
    Task<OcrResult> PerformOcrOnImageAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Realiza OCR sobre un PDF escaneado (renderizando primero las páginas con Google PDFium y extrayendo texto con Tesseract).
    /// </summary>
    Task<OcrResult> PerformOcrOnScannedPdfAsync(byte[] pdfBytes, OcrOptions? options = null, CancellationToken cancellationToken = default);
}
