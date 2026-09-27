using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfRepairService
{
    Task<PdfRepairResult> RepairPdfAsync(byte[] corruptedPdf, CancellationToken cancellationToken = default);
}
