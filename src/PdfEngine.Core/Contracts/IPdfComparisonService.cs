using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfComparisonService
{
    Task<ComparisonResult> ComparePagesAsync(byte[] pdfDocumentA, byte[] pdfDocumentB, ComparisonOptions options, CancellationToken cancellationToken = default);
}
