using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfBatchService
{
    Task<BatchProcessResult> ProcessBatchAsync(
        IEnumerable<(string FileName, byte[] Bytes)> files,
        BatchOperationOptions options,
        CancellationToken cancellationToken = default);
}
