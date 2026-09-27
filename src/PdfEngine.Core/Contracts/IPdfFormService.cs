using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfFormService
{
    Task<List<PdfFormField>> InspectFormsAsync(byte[] sourcePdf, CancellationToken cancellationToken = default);
    Task<FormFlattenResult> FlattenFormsAsync(byte[] sourcePdf, CancellationToken cancellationToken = default);
}
