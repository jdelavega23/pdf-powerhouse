using PdfEngine.Core.Contracts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfSplitService : IPdfSplitService
{
    public Task<IReadOnlyList<byte[]>> SplitAllPagesAsync(byte[] pdfDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run<IReadOnlyList<byte[]>>(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var inputDocument = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

            var result = new List<byte[]>(inputDocument.PageCount);

            for (int i = 0; i < inputDocument.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var singlePageDoc = new PdfDocument();
                singlePageDoc.AddPage(inputDocument.Pages[i]);

                using var outStream = new MemoryStream();
                singlePageDoc.Save(outStream, false);
                result.Add(outStream.ToArray());
            }

            return result;
        }, cancellationToken);
    }

    public Task<byte[]> ExtractPagesAsync(byte[] pdfDocument, string pageRange, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageRange);

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var inputDocument = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

            var pageNumbers = ParsePageRanges(pageRange, inputDocument.PageCount);
            if (pageNumbers.Count == 0)
            {
                throw new ArgumentException($"No valid pages selected by range '{pageRange}'. Document has {inputDocument.PageCount} pages.", nameof(pageRange));
            }

            using var outputDocument = new PdfDocument();

            foreach (var pageNum in pageNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pageIndex = pageNum - 1;
                if (pageIndex >= 0 && pageIndex < inputDocument.PageCount)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }
            }

            using var outStream = new MemoryStream();
            outputDocument.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> RemovePagesAsync(byte[] pdfDocument, IEnumerable<int> pageNumbers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(pageNumbers);

        return Task.Run(() =>
        {
            var pagesToRemove = new HashSet<int>(pageNumbers);

            using var inputStream = new MemoryStream(pdfDocument);
            using var inputDocument = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

            using var outputDocument = new PdfDocument();

            for (int i = 0; i < inputDocument.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pageNum = i + 1; // 1-based

                if (!pagesToRemove.Contains(pageNum))
                {
                    outputDocument.AddPage(inputDocument.Pages[i]);
                }
            }

            if (outputDocument.PageCount == 0)
            {
                throw new InvalidOperationException("Cannot remove all pages from the document.");
            }

            using var outStream = new MemoryStream();
            outputDocument.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public static List<int> ParsePageRanges(string pageRange, int maxPages)
    {
        var result = new List<int>();
        var parts = pageRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var rangeParts = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out int start) &&
                    int.TryParse(rangeParts[1], out int end))
                {
                    int min = Math.Max(1, Math.Min(start, end));
                    int max = Math.Min(maxPages, Math.Max(start, end));

                    for (int p = min; p <= max; p++)
                    {
                        if (!result.Contains(p)) result.Add(p);
                    }
                }
            }
            else if (int.TryParse(part, out int singlePage))
            {
                if (singlePage >= 1 && singlePage <= maxPages && !result.Contains(singlePage))
                {
                    result.Add(singlePage);
                }
            }
        }

        return result;
    }
}
