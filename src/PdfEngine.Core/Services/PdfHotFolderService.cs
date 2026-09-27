using System.Collections.Concurrent;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;

namespace PdfEngine.Core.Services;

public sealed class PdfHotFolderService : IPdfHotFolderService, IDisposable
{
    private readonly IPdfAService _pdfAService;
    private readonly IPdfCompressionService _compressionService;
    private readonly IPdfRepairService _repairService;
    private readonly IPdfFormService _formService;
    private readonly IPdfOfficeConverterService _officeConverterService;

    private HotFolderConfiguration _config = new();
    private bool _isActive = false;
    private int _processedCount = 0;
    private int _failedCount = 0;
    private readonly ConcurrentQueue<HotFolderLogEntry> _logs = new();
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private readonly object _lock = new();

    public PdfHotFolderService(
        IPdfAService pdfAService,
        IPdfCompressionService compressionService,
        IPdfRepairService repairService,
        IPdfFormService formService,
        IPdfOfficeConverterService officeConverterService)
    {
        _pdfAService = pdfAService;
        _compressionService = compressionService;
        _repairService = repairService;
        _formService = formService;
        _officeConverterService = officeConverterService;

        Log("INFO", "Servicio Watchdog de Carpetas Calientes inicializado.");
        EnsureDirectories();
    }

    public HotFolderStatus GetStatus()
    {
        return new HotFolderStatus
        {
            IsActive = _isActive,
            Configuration = _config,
            ProcessedCount = _processedCount,
            FailedCount = _failedCount,
            RecentLogs = _logs.ToArray().Reverse().Take(50).ToList()
        };
    }

    public void Configure(HotFolderConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lock (_lock)
        {
            _config = configuration;
            EnsureDirectories();
            Log("INFO", $"Configuración actualizada. Acción: {_config.Action}, Intervalo: {_config.PollingIntervalSeconds}s");
            if (_config.Enabled && !_isActive)
            {
                Start();
            }
            else if (!_config.Enabled && _isActive)
            {
                Pause();
            }
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isActive) return;
            _isActive = true;
            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(() => PollingLoopAsync(_cts.Token));
            Log("SUCCESS", $"Watchdog iniciado. Vigilando directorio: {_config.InputPath}");
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isActive) return;
            _isActive = false;
            _cts?.Cancel();
            Log("WARN", "Watchdog en pausa.");
        }
    }

    public async Task<int> TriggerScanAsync(CancellationToken cancellationToken = default)
    {
        return await ProcessFilesInternalAsync(cancellationToken);
    }

    private async Task PollingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessFilesInternalAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log("ERROR", $"Error en ciclo de sondeo: {ex.Message}");
            }

            try
            {
                int delaySec = Math.Max(1, _config.PollingIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<int> ProcessFilesInternalAsync(CancellationToken ct)
    {
        EnsureDirectories();
        if (!Directory.Exists(_config.InputPath)) return 0;

        var files = Directory.GetFiles(_config.InputPath);
        int count = 0;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith(".")) continue;

            // Wait a moment if file is still being copied/written
            if (!IsFileReady(filePath))
            {
                continue;
            }

            Log("INFO", $"Detectado nuevo archivo: '{fileName}'. Procesando con acción: {_config.Action}...");

            try
            {
                byte[] inputBytes = await File.ReadAllBytesAsync(filePath, ct);
                byte[] outputBytes;
                string outputExtension = ".pdf";

                switch (_config.Action)
                {
                    case HotFolderAction.ConvertToPdfA:
                        var pdfaRes = await _pdfAService.ConvertToPdfAAsync(inputBytes, PdfAProfile.PdfA1b, ct);
                        outputBytes = pdfaRes.ConvertedPdf;
                        break;

                    case HotFolderAction.Compress:
                        var compRes = await _compressionService.CompressPdfAsync(inputBytes, new CompressionOptions(), ct);
                        outputBytes = compRes.CompressedPdf;
                        break;

                    case HotFolderAction.Repair:
                        var repRes = await _repairService.RepairPdfAsync(inputBytes, ct);
                        outputBytes = repRes.RepairedPdf;
                        break;

                    case HotFolderAction.FlattenForms:
                        var flatRes = await _formService.FlattenFormsAsync(inputBytes, ct);
                        outputBytes = flatRes.FlattenedPdf;
                        break;

                    case HotFolderAction.OfficeToPdf:
                        var offRes = await _officeConverterService.ConvertToPdfAsync(fileName, inputBytes, null, ct);
                        if (!offRes.Success) throw new InvalidOperationException(offRes.ErrorMessage ?? "Error en conversión Office.");
                        outputBytes = offRes.PdfBytes;
                        break;

                    default:
                        outputBytes = inputBytes;
                        break;
                }

                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var outName = $"{baseName}_{_config.Action.ToString().ToLower()}{outputExtension}";
                var targetPath = Path.Combine(_config.ProcessedPath, outName);

                await File.WriteAllBytesAsync(targetPath, outputBytes, ct);

                // Safely remove processed input file
                File.Delete(filePath);

                Interlocked.Increment(ref _processedCount);
                count++;
                Log("SUCCESS", $"Archivo '{fileName}' procesado con éxito -> Guardado en: '{outName}'");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedCount);
                Log("ERROR", $"Fallo al procesar '{fileName}': {ex.Message}");

                try
                {
                    var failTarget = Path.Combine(_config.FailedPath, fileName);
                    if (File.Exists(failTarget)) File.Delete(failTarget);
                    File.Move(filePath, failTarget);
                }
                catch { }
            }
        }

        return count;
    }

    private void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(_config.InputPath)) Directory.CreateDirectory(_config.InputPath);
            if (!Directory.Exists(_config.ProcessedPath)) Directory.CreateDirectory(_config.ProcessedPath);
            if (!Directory.Exists(_config.FailedPath)) Directory.CreateDirectory(_config.FailedPath);
        }
        catch { }
    }

    private static bool IsFileReady(string filename)
    {
        try
        {
            using var inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            return inputStream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void Log(string level, string message)
    {
        _logs.Enqueue(new HotFolderLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        });

        while (_logs.Count > 100)
        {
            _logs.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
