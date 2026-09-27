using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfTransformService
{
    Task<byte[]> RotatePagesAsync(byte[] pdfDocument, int angleDegrees, IEnumerable<int>? targetPageNumbers = null, CancellationToken cancellationToken = default);
    Task<byte[]> ApplyWatermarkAsync(byte[] pdfDocument, WatermarkOptions options, CancellationToken cancellationToken = default);
    Task<byte[]> ReorderPagesAsync(byte[] pdfDocument, IReadOnlyList<int> newPageOrder, CancellationToken cancellationToken = default);
}
