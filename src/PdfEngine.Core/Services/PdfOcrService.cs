using System.Diagnostics;
using System.Text;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using Tesseract;

namespace PdfEngine.Core.Services;

public class PdfOcrService : IPdfOcrService
{
    private readonly IPdfInspectionService _inspectionService;
    private readonly string _tessDataDir;
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly SemaphoreSlim _downloadLock = new SemaphoreSlim(1, 1);

    public PdfOcrService(IPdfInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
        _tessDataDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(_tessDataDir))
        {
            Directory.CreateDirectory(_tessDataDir);
        }

        // Auto-discover and copy system installed models
        var systemPaths = new[]
        {
            "/app/tessdata",
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tessdata",
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
        };

        foreach (var dir in systemPaths)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.traineddata"))
                    {
                        var dest = Path.Combine(_tessDataDir, Path.GetFileName(file));
                        if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                        {
                            File.Copy(file, dest, true);
                        }
                    }
                }
                catch { }
            }
        }
    }

    public async Task<OcrResult> PerformOcrOnImageAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        options ??= new OcrOptions();

        var sw = Stopwatch.StartNew();
        var primaryLang = options.Language.Split('+')[0];

        // Ensure language traineddata model is available locally
        await EnsureLanguageModelAsync(primaryLang, cancellationToken);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var engine = new TesseractEngine(_tessDataDir, primaryLang, EngineMode.Default);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);

            var text = page.GetText()?.Trim() ?? string.Empty;
            var confidence = page.GetMeanConfidence();
            var words = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

            sw.Stop();

            return new OcrResult
            {
                Text = text,
                MeanConfidence = confidence * 100, // percentage
                WordCount = words,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }, cancellationToken);
    }

    public async Task<OcrResult> PerformOcrOnScannedPdfAsync(byte[] pdfBytes, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        options ??= new OcrOptions();

        var sw = Stopwatch.StartNew();

        // 1. Inspect metadata to know page count
        var metadata = await _inspectionService.InspectMetadataAsync(pdfBytes, cancellationToken: cancellationToken);
        var aggregatedText = new StringBuilder();
        float totalConfidence = 0;
        int processedPages = 0;
        int totalWords = 0;

        for (int i = 0; i < metadata.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 2. High-fidelity rendering via Google PDFium native engine
            var pageImagePng = await _inspectionService.RenderPageToPngAsync(pdfBytes, i, dpi: options.Dpi, cancellationToken: cancellationToken);

            // 3. OCR on rendered page image
            var pageOcr = await PerformOcrOnImageAsync(pageImagePng, options, cancellationToken);

            var pageText = pageOcr.Text;
            var pageConfidence = pageOcr.MeanConfidence;

            // 4. Hybrid Intelligent Fallback: if OCR found very few words, extract native digital text
            if (string.IsNullOrWhiteSpace(pageText) || pageOcr.WordCount < 3)
            {
                try
                {
                    var digitalText = await _inspectionService.ExtractPageTextAsync(pdfBytes, i, null, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(digitalText) && digitalText.Trim().Length > pageText.Length)
                    {
                        pageText = digitalText.Trim();
                        pageConfidence = 99.0f; // High confidence for digital text
                    }
                }
                catch { }
            }

            var words = pageText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

            aggregatedText.AppendLine($"--- PÁGINA {i + 1} ---");
            aggregatedText.AppendLine(pageText);
            aggregatedText.AppendLine();

            totalConfidence += pageConfidence;
            totalWords += words;
            processedPages++;
        }

        sw.Stop();

        return new OcrResult
        {
            Text = aggregatedText.ToString().Trim(),
            MeanConfidence = processedPages > 0 ? (totalConfidence / processedPages) : 0,
            WordCount = totalWords,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
    }

    private async Task EnsureLanguageModelAsync(string lang, CancellationToken cancellationToken)
    {
        var targetFile = Path.Combine(_tessDataDir, $"{lang}.traineddata");
        if (File.Exists(targetFile) && new FileInfo(targetFile).Length > 1000)
        {
            return;
        }

        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(targetFile) && new FileInfo(targetFile).Length > 1000)
            {
                return;
            }

            // Download fast traineddata model from official Tesseract GitHub repository
            var downloadUrl = $"https://github.com/tesseract-ocr/tessdata_fast/raw/main/{lang}.traineddata";
            using var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback to eng if requested language is not found
                if (lang != "eng")
                {
                    await EnsureLanguageModelAsync("eng", cancellationToken);
                    return;
                }
                throw new InvalidOperationException($"No se pudo descargar el modelo de lenguaje '{lang}' para Tesseract desde {downloadUrl}.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(targetFile, bytes, cancellationToken);
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
