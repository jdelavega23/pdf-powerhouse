using PdfEngine.Core.Contracts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfMergeService : IPdfMergeService
{
    public Task<byte[]> MergeAsync(IReadOnlyList<byte[]> pdfDocuments, CancellationToken cancellationToken = default)
    {
        if (pdfDocuments == null || pdfDocuments.Count == 0)
        {
            throw new ArgumentException("At least one PDF document must be provided to merge.", nameof(pdfDocuments));
        }

        return Task.Run(() =>
        {
            using var outputDocument = new PdfDocument();

            foreach (var docBytes in pdfDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (docBytes == null || docBytes.Length == 0) continue;

                using var stream = new MemoryStream(docBytes);
                using var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputDocument.AddPage(inputDocument.Pages[i]);
                }
            }

            using var outStream = new MemoryStream();
            outputDocument.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> MergeStreamsAsync(IReadOnlyList<Stream> pdfStreams, CancellationToken cancellationToken = default)
    {
        if (pdfStreams == null || pdfStreams.Count == 0)
        {
            throw new ArgumentException("At least one stream must be provided to merge.", nameof(pdfStreams));
        }

        return Task.Run(() =>
        {
            using var outputDocument = new PdfDocument();

            foreach (var stream in pdfStreams)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputDocument.AddPage(inputDocument.Pages[i]);
                }
            }

            using var outStream = new MemoryStream();
            outputDocument.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }
}
