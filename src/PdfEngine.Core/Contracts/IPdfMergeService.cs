namespace PdfEngine.Core.Contracts;

public interface IPdfMergeService
{
    Task<byte[]> MergeAsync(IReadOnlyList<byte[]> pdfDocuments, CancellationToken cancellationToken = default);
    Task<byte[]> MergeStreamsAsync(IReadOnlyList<Stream> pdfStreams, CancellationToken cancellationToken = default);
}
