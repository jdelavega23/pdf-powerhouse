using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfHotFolderService
{
    HotFolderStatus GetStatus();
    void Configure(HotFolderConfiguration configuration);
    void Start();
    void Pause();
    Task<int> TriggerScanAsync(CancellationToken cancellationToken = default);
}
