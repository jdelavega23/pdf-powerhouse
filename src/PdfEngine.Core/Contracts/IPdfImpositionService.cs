using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfImpositionService
{
    Task<byte[]> GenerateImpositionAsync(byte[] sourcePdf, ImpositionOptions options, CancellationToken cancellationToken = default);
}
