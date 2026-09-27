using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfStructuredExtractorService
{
    Task<StructuredDocumentContent> ExtractStructuredContentAsync(byte[] pdfDocument, CancellationToken cancellationToken = default);
}
