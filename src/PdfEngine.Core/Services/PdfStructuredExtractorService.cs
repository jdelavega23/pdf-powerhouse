using System.Text;
using System.Text.RegularExpressions;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using UglyToad.PdfPig;

namespace PdfEngine.Core.Services;

public partial class PdfStructuredExtractorService : IPdfStructuredExtractorService
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>""]+|www\.[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<StructuredDocumentContent> ExtractStructuredContentAsync(byte[] pdfDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfDocument);
            var pages = new List<StructuredPageContent>();
            var allUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plainTextBuilder = new StringBuilder();
            var markdownBuilder = new StringBuilder();

            int totalWords = 0;
            int totalChars = 0;

            markdownBuilder.AppendLine("# Contenido Estructurado del Documento");
            markdownBuilder.AppendLine();

            foreach (var page in doc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawText = page.Text ?? string.Empty;
                var words = page.GetWords().ToList();
                int wordCount = words.Count;
                int charCount = rawText.Length;

                totalWords += wordCount;
                totalChars += charCount;

                var pageUrls = new List<string>();
                var matches = UrlRegex.Matches(rawText);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        var url = match.Value;
                        pageUrls.Add(url);
                        allUrls.Add(url);
                    }
                }

                pages.Add(new StructuredPageContent
                {
                    PageNumber = page.Number,
                    Text = rawText,
                    WordCount = wordCount,
                    CharacterCount = charCount,
                    ExtractedUrls = pageUrls
                });

                plainTextBuilder.AppendLine($"--- PÁGINA {page.Number} ---");
                plainTextBuilder.AppendLine(rawText);
                plainTextBuilder.AppendLine();

                markdownBuilder.AppendLine($"## Página {page.Number}  *(Palabras: {wordCount})*");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(rawText);
                markdownBuilder.AppendLine();

                if (pageUrls.Count > 0)
                {
                    markdownBuilder.AppendLine("**Enlaces detectados:**");
                    foreach (var url in pageUrls)
                    {
                        markdownBuilder.AppendLine($"- [{url}]({url})");
                    }
                    markdownBuilder.AppendLine();
                }
            }

            double readingMinutes = totalWords > 0 ? Math.Round(totalWords / 200.0, 1) : 0.0;

            return new StructuredDocumentContent
            {
                TotalPages = doc.NumberOfPages,
                TotalWords = totalWords,
                TotalCharacters = totalChars,
                EstimatedReadingMinutes = readingMinutes,
                AllExtractedUrls = allUrls.ToList(),
                Pages = pages,
                FullPlainText = plainTextBuilder.ToString(),
                FullMarkdownText = markdownBuilder.ToString()
            };
        }, cancellationToken);
    }
}
