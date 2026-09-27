using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfTableExtractorService
{
    Task<TableExtractionResult> ExtractTablesAsync(byte[] pdfDocument, TableExtractionOptions? options = null, CancellationToken cancellationToken = default);
    Task<string> ExtractCsvAsync(byte[] pdfDocument, TableExtractionOptions? options = null, CancellationToken cancellationToken = default);
}
