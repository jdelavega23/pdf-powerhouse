using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfPageNumberService
{
    Task<byte[]> AddPageNumbersAsync(byte[] pdfDocument, PageNumberOptions options, CancellationToken cancellationToken = default);
}
