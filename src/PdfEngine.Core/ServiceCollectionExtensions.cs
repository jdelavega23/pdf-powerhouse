using Microsoft.Extensions.DependencyInjection;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Services;

namespace PdfEngine.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPdfEngine(this IServiceCollection services)
    {
        services.AddSingleton<IPdfMergeService, PdfMergeService>();
        services.AddSingleton<IPdfSplitService, PdfSplitService>();
        services.AddSingleton<IPdfTransformService, PdfTransformService>();
        services.AddSingleton<IPdfSecurityService, PdfSecurityService>();
        services.AddSingleton<IPdfInspectionService, PdfInspectionService>();
        services.AddSingleton<IPdfSigningService, PdfSigningService>();
        services.AddSingleton<IPdfOcrService, PdfOcrService>();
        services.AddSingleton<IPdfEditorService, PdfEditorService>();
        services.AddSingleton<IPdfCompressionService, PdfCompressionService>();
        services.AddSingleton<IPdfConverterService, PdfConverterService>();
        services.AddSingleton<IPdfPageNumberService, PdfPageNumberService>();
        services.AddSingleton<IPdfRedactionService, PdfRedactionService>();
        services.AddSingleton<IPdfComparisonService, PdfComparisonService>();
        services.AddSingleton<IPdfStructuredExtractorService, PdfStructuredExtractorService>();
        services.AddSingleton<IPdfRepairService, PdfRepairService>();
        services.AddSingleton<IPdfImpositionService, PdfImpositionService>();
        services.AddSingleton<IPdfFormService, PdfFormService>();
        services.AddSingleton<IPdfBatchService, PdfBatchService>();
        services.AddSingleton<IPdfAService, PdfAService>();
        services.AddSingleton<IPdfTableExtractorService, PdfTableExtractorService>();
        services.AddSingleton<IPdfOfficeConverterService, PdfOfficeConverterService>();
        services.AddSingleton<IPdfHotFolderService, PdfHotFolderService>();
        services.AddSingleton<IPdfInvoiceService, PdfInvoiceService>();
        services.AddSingleton<IPdfFormBuilderService, PdfFormBuilderService>();

        return services;
    }
}
