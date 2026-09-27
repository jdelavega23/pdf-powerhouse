using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfInspectionService
{
    Task<PdfMetadata> InspectMetadataAsync(byte[] pdfDocument, string? password = null, CancellationToken cancellationToken = default);
    Task<byte[]> RenderPageToPngAsync(byte[] pdfDocument, int pageIndex, int dpi = 150, string? password = null, CancellationToken cancellationToken = default);
    Task<string> ExtractPageTextAsync(byte[] pdfDocument, int pageIndex, string? password = null, CancellationToken cancellationToken = default);
}
