using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfEditorService
{
    /// <summary>
    /// Inserta un texto en la posición indicada sobre una página del PDF.
    /// </summary>
    Task<byte[]> AddTextAnnotationAsync(byte[] pdfDocument, TextAnnotationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta una imagen (sello, firma manuscrita transparente, logotipo) sobre una página del PDF.
    /// </summary>
    Task<byte[]> AddImageAnnotationAsync(byte[] pdfDocument, ImageAnnotationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica una caja de redacción/censura sobre el área especificada, oscureciendo permanentemente la zona confidencial.
    /// </summary>
    Task<byte[]> ApplyRedactionAsync(byte[] pdfDocument, RedactionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica múltiples ediciones visuales por lotes (textos, imágenes, firmas dibujadas, censuras) en una sola pasada.
    /// </summary>
    Task<byte[]> BatchEditAsync(byte[] pdfDocument, IReadOnlyList<BatchEditItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrae todos los bloques de texto existentes en una página con sus cajas delimitadoras (X, Y, Ancho, Alto), tamaño de fuente y color para permitir su edición interactiva en el lienzo.
    /// </summary>
    Task<IReadOnlyList<PdfTextBlock>> ExtractTextBlocksAsync(byte[] pdfDocument, int pageNumber, CancellationToken cancellationToken = default);
}
