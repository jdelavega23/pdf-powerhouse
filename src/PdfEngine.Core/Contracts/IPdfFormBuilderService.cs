using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfFormBuilderService
{
    Task<FormBuildResult> BuildInteractiveFormAsync(FormBuildRequest request, CancellationToken cancellationToken = default);
    Task<FormBuildResult> CreateStandardRegistrationFormAsync(string companyName, string documentTitle, CancellationToken cancellationToken = default);
}
