using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;

namespace PdfEngine.Core.Services;

public class PdfBatchService : IPdfBatchService
{
    private readonly IPdfCompressionService _compressionService;
    private readonly IPdfTransformService _transformService;
    private readonly IPdfPageNumberService _pageNumberService;
    private readonly IPdfRepairService _repairService;
    private readonly IPdfFormService _formService;

    public PdfBatchService(
        IPdfCompressionService compressionService,
        IPdfTransformService transformService,
        IPdfPageNumberService pageNumberService,
        IPdfRepairService repairService,
        IPdfFormService formService)
    {
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
        _pageNumberService = pageNumberService ?? throw new ArgumentNullException(nameof(pageNumberService));
        _repairService = repairService ?? throw new ArgumentNullException(nameof(repairService));
        _formService = formService ?? throw new ArgumentNullException(nameof(formService));
    }

    public async Task<BatchProcessResult> ProcessBatchAsync(
        IEnumerable<(string FileName, byte[] Bytes)> files,
        BatchOperationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);

        var fileList = files.ToList();
        var results = new ConcurrentBag<BatchFileResult>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(fileList, parallelOptions, async (item, ct) =>
        {
            var fileResult = new BatchFileResult
            {
                FileName = item.FileName,
                OriginalSizeBytes = item.Bytes.Length
            };

            try
            {
                byte[] processedBytes;

                switch (options.Operation)
                {
                    case BatchOperationType.Compress:
                        var compRes = await _compressionService.CompressPdfAsync(
                            item.Bytes,
                            new CompressionOptions { Level = options.CompressionLevel },
                            ct);
                        processedBytes = compRes.CompressedPdf;
                        fileResult.Message = $"Compresión exitosa ({compRes.SavedPercentage}% ahorrado).";
                        break;

                    case BatchOperationType.Watermark:
                        processedBytes = await _transformService.ApplyWatermarkAsync(
                            item.Bytes,
                            new WatermarkOptions
                            {
                                Text = string.IsNullOrWhiteSpace(options.WatermarkText) ? "CONFIDENCIAL" : options.WatermarkText,
                                FontColorHex = options.WatermarkColorHex ?? "#FF0000",
                                Opacity = options.WatermarkOpacity ?? 0.3,
                                FontSize = options.WatermarkFontSize ?? 48,
                                RotationDegrees = options.WatermarkRotation ?? -45
                            },
                            ct);
                        fileResult.Message = "Marca de agua estampada en todas las páginas.";
                        break;

                    case BatchOperationType.Numbering:
                        processedBytes = await _pageNumberService.AddPageNumbersAsync(
                            item.Bytes,
                            new PageNumberOptions
                            {
                                Position = options.NumberPosition,
                                Format = options.NumberFormat ?? "Página {n} de {total}"
                            },
                            ct);
                        fileResult.Message = "Numeración de páginas aplicada.";
                        break;

                    case BatchOperationType.Repair:
                        var repRes = await _repairService.RepairPdfAsync(item.Bytes, ct);
                        processedBytes = repRes.RepairedPdf;
                        fileResult.Message = repRes.DiagnosticMessage;
                        break;

                    case BatchOperationType.FlattenForms:
                        var flatRes = await _formService.FlattenFormsAsync(item.Bytes, ct);
                        processedBytes = flatRes.FlattenedPdf;
                        fileResult.Message = $"Se aplanaron {flatRes.FlattenedFieldsCount} campos interactivos.";
                        break;

                    case BatchOperationType.Rotate:
                        processedBytes = await _transformService.RotatePagesAsync(item.Bytes, options.RotateDegrees, null, ct);
                        fileResult.Message = $"Páginas rotadas {options.RotateDegrees} grados.";
                        break;

                    default:
                        throw new NotSupportedException($"Operación por lotes '{options.Operation}' no soportada.");
                }

                fileResult.Success = processedBytes.Length > 0;
                fileResult.ProcessedSizeBytes = processedBytes.Length;
                fileResult.ProcessedBytes = processedBytes;
            }
            catch (Exception ex)
            {
                fileResult.Success = false;
                fileResult.Message = $"Error: {ex.Message}";
                fileResult.ProcessedSizeBytes = 0;
                fileResult.ProcessedBytes = Array.Empty<byte>();
            }

            results.Add(fileResult);
        });

        var orderedResults = results.OrderBy(r => r.FileName).ToList();

        // Build ZIP Package
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("=================================================");
            reportBuilder.AppendLine(" PDF POWERHOUSE - INFORME DE PROCESAMIENTO POR LOTES");
            reportBuilder.AppendLine($" Operación Realizada: {options.Operation}");
            reportBuilder.AppendLine($" Fecha de Procesamiento: {DateTime.UtcNow:s}Z");
            reportBuilder.AppendLine("=================================================");
            reportBuilder.AppendLine();

            foreach (var r in orderedResults)
            {
                reportBuilder.AppendLine($"Archivo: {r.FileName}");
                reportBuilder.AppendLine($"Estado: {(r.Success ? "EXITOSO" : "FALLIDO")}");
                reportBuilder.AppendLine($"Detalle: {r.Message}");
                reportBuilder.AppendLine($"Tamaño Original: {r.OriginalSizeBytes:N0} bytes");
                reportBuilder.AppendLine($"Tamaño Procesado: {r.ProcessedSizeBytes:N0} bytes");
                reportBuilder.AppendLine("-------------------------------------------------");

                if (r.Success && r.ProcessedBytes.Length > 0)
                {
                    string entryName = $"{Path.GetFileNameWithoutExtension(r.FileName)}_{options.Operation.ToString().ToLowerInvariant()}.pdf";
                    var entry = archive.CreateEntry(entryName, CompressionLevelOption(options.Operation));
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(r.ProcessedBytes, cancellationToken);
                }
            }

            var reportEntry = archive.CreateEntry("INFORME_LOTE.txt", System.IO.Compression.CompressionLevel.Optimal);
            using var reportStream = reportEntry.Open();
            var reportBytes = Encoding.UTF8.GetBytes(reportBuilder.ToString());
            await reportStream.WriteAsync(reportBytes, cancellationToken);
        }

        return new BatchProcessResult
        {
            TotalFiles = orderedResults.Count,
            SuccessfulFiles = orderedResults.Count(r => r.Success),
            FailedFiles = orderedResults.Count(r => !r.Success),
            Results = orderedResults,
            ZipArchiveBytes = zipStream.ToArray()
        };
    }

    private static System.IO.Compression.CompressionLevel CompressionLevelOption(BatchOperationType op)
    {
        return op == BatchOperationType.Compress 
            ? System.IO.Compression.CompressionLevel.Optimal 
            : System.IO.Compression.CompressionLevel.Fastest;
    }
}
