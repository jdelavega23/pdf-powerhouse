using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfCompressionService
{
    Task<CompressionResult> CompressPdfAsync(byte[] pdfDocument, CompressionOptions options, CancellationToken cancellationToken = default);
}
