namespace PdfEngine.Core.Contracts;

public interface IPdfSplitService
{
    Task<IReadOnlyList<byte[]>> SplitAllPagesAsync(byte[] pdfDocument, CancellationToken cancellationToken = default);
    Task<byte[]> ExtractPagesAsync(byte[] pdfDocument, string pageRange, CancellationToken cancellationToken = default);
    Task<byte[]> RemovePagesAsync(byte[] pdfDocument, IEnumerable<int> pageNumbers, CancellationToken cancellationToken = default);
}
