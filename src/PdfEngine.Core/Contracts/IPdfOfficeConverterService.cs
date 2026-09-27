using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfOfficeConverterService
{
    Task<OfficeConversionResult> ConvertToPdfAsync(string fileName, byte[] fileBytes, OfficeConversionOptions? options = null, CancellationToken cancellationToken = default);
    Task<bool> IsLibreOfficeAvailableAsync(CancellationToken cancellationToken = default);
}
