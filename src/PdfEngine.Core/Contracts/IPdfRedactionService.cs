using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfRedactionService
{
    Task<byte[]> RedactPdfAsync(byte[] pdfDocument, DeepRedactionOptions options, CancellationToken cancellationToken = default);
}
