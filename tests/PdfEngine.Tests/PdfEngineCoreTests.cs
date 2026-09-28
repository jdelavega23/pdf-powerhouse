using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfEngine.Core.Services;

namespace PdfEngine.Tests;

public class PdfEngineCoreTests
{
    private readonly IPdfMergeService _mergeService = new PdfMergeService();
    private readonly IPdfSplitService _splitService = new PdfSplitService();
    private readonly IPdfTransformService _transformService = new PdfTransformService();
    private readonly IPdfSecurityService _securityService = new PdfSecurityService();
    private readonly IPdfInspectionService _inspectionService = new PdfInspectionService();

    [Fact]
    public async Task Merge_TwoPdfs_ShouldCombineAllPagesCorrectly()
    {
        // Arrange
        var doc1 = PdfTestHelper.CreateSamplePdf(2, "Doc A");
        var doc2 = PdfTestHelper.CreateSamplePdf(3, "Doc B");

        // Act
        var mergedBytes = await _mergeService.MergeAsync(new[] { doc1, doc2 });
        var metadata = await _inspectionService.InspectMetadataAsync(mergedBytes);

        // Assert
        Assert.NotNull(mergedBytes);
        Assert.True(mergedBytes.Length > 0);
        Assert.Equal(5, metadata.PageCount);
    }

    [Fact]
    public async Task Split_ShouldExtractIndividualPages()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(4, "Doc Split");

        // Act
        var pages = await _splitService.SplitAllPagesAsync(doc);

        // Assert
        Assert.Equal(4, pages.Count);
        foreach (var pageBytes in pages)
        {
            var meta = await _inspectionService.InspectMetadataAsync(pageBytes);
            Assert.Equal(1, meta.PageCount);
        }
    }

    [Fact]
    public async Task ExtractPages_WithPageRanges_ShouldReturnSpecifiedPages()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(5, "Doc Range");

        // Act - extract pages 1, 3, and 5
        var extracted = await _splitService.ExtractPagesAsync(doc, "1, 3, 5");
        var meta = await _inspectionService.InspectMetadataAsync(extracted);

        // Assert
        Assert.Equal(3, meta.PageCount);
    }

    [Fact]
    public async Task RotatePages_ShouldModifyPageOrientation()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(2, "Doc Rotate");

        // Act - rotate 90 degrees
        var rotated = await _transformService.RotatePagesAsync(doc, 90);
        var meta = await _inspectionService.InspectMetadataAsync(rotated);

        // Assert
        Assert.Equal(90, meta.Pages[0].Rotation);
        Assert.Equal(90, meta.Pages[1].Rotation);
    }

    [Fact]
    public async Task ApplyWatermark_ShouldSucceedAndPreservePageStructure()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(2, "Doc Watermark");
        var watermarkOpts = new WatermarkOptions
        {
            Text = "JUAN MANUEL PROYECTO TOP",
            FontSize = 36,
            Opacity = 0.4,
            RotationDegrees = -45
        };

        // Act
        var watermarked = await _transformService.ApplyWatermarkAsync(doc, watermarkOpts);
        var meta = await _inspectionService.InspectMetadataAsync(watermarked);

        // Assert
        Assert.NotNull(watermarked);
        Assert.Equal(2, meta.PageCount);
    }

    [Fact]
    public async Task ProtectAndUnlockPdf_WithPassword_ShouldSecureAndRestore()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(1, "Doc Security");
        var protectOpts = new PdfProtectionOptions
        {
            UserPassword = "SecretPassword123!",
            OwnerPassword = "AdminPassword456!",
            PermitPrint = true,
            PermitCopyContent = false
        };

        // Act - Protect
        var protectedBytes = await _securityService.ProtectPdfAsync(doc, protectOpts);
        Assert.NotNull(protectedBytes);

        // Act - Unlock
        var unlockedBytes = await _securityService.UnlockPdfAsync(protectedBytes, "SecretPassword123!");
        var unlockedMeta = await _inspectionService.InspectMetadataAsync(unlockedBytes);

        // Assert
        Assert.Equal(1, unlockedMeta.PageCount);
        Assert.False(unlockedMeta.IsEncrypted);
    }

    [Fact]
    public async Task InspectionService_RenderPageToPng_UsingPdfium_ShouldGenerateValidPng()
    {
        // Arrange
        var doc = PdfTestHelper.CreateSamplePdf(1, "Doc PDFium Render");

        // Act - render page 0 (1st page) at 100 DPI
        var pngBytes = await _inspectionService.RenderPageToPngAsync(doc, 0, dpi: 100);

        // Assert: Valid PNG starts with signature bytes 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 8);
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal((byte)'P', pngBytes[1]);
        Assert.Equal((byte)'N', pngBytes[2]);
        Assert.Equal((byte)'G', pngBytes[3]);
    }

    [Fact]
    public async Task GenerateDevelopmentCertificate_ShouldProduceValidX509Pfx()
    {
        // Arrange
        var signingService = new PdfSigningService();

        // Act
        var cert = await signingService.GenerateDevelopmentCertificateAsync("Juan Manuel de la Vega", "TestPass123!");

        // Assert
        Assert.NotNull(cert);
        Assert.True(cert.PfxBytes.Length > 0);
        Assert.Contains("Juan Manuel de la Vega", cert.Subject);
        Assert.False(string.IsNullOrEmpty(cert.Thumbprint));
    }

    [Fact]
    public async Task SignPdf_And_VerifySignature_ShouldValidateIntegrity()
    {
        // Arrange
        var signingService = new PdfSigningService();
        var doc = PdfTestHelper.CreateSamplePdf(2, "Doc Contrato Legal");

        var signOpts = new DigitalSignatureOptions
        {
            SignerName = "Juan Manuel de la Vega",
            Reason = "Aprobado y Conforme con el Contrato",
            Location = "Madrid, España",
            IncludeVisualStamp = true,
            TargetPageNumber = 1
        };

        // Act - Sign
        var signedPdf = await signingService.SignPdfAsync(doc, signOpts);
        Assert.NotNull(signedPdf);
        Assert.True(signedPdf.Length > doc.Length);

        // Act - Verify
        var verification = await signingService.VerifySignatureAsync(signedPdf);

        // Assert
        Assert.True(verification.IsSigned);
        Assert.True(verification.IsValid);
        Assert.Equal("Juan Manuel de la Vega", verification.SignerName);
        Assert.NotNull(verification.SigningTime);
        Assert.False(string.IsNullOrEmpty(verification.DocumentHash));
    }

    [Fact]
    public async Task AddTextAnnotation_ShouldModifyDocumentContent()
    {
        // Arrange
        var editorService = new PdfEditorService();
        var doc = PdfTestHelper.CreateSamplePdf(1, "Doc Edicion");

        var textOpts = new TextAnnotationOptions
        {
            PageNumber = 1,
            PositionX = 120,
            PositionY = 200,
            Text = "Texto agregado por el Editor de Juan Manuel",
            FontSize = 16,
            FontColorHex = "#FF0000",
            IsBold = true
        };

        // Act
        var editedPdf = await editorService.AddTextAnnotationAsync(doc, textOpts);

        // Assert
        Assert.NotNull(editedPdf);
        Assert.True(editedPdf.Length > doc.Length);
    }

    [Fact]
    public async Task ApplyRedaction_ShouldApplySolidBlock()
    {
        // Arrange
        var editorService = new PdfEditorService();
        var doc = PdfTestHelper.CreateSamplePdf(1, "Doc Censura");

        var redactOpts = new RedactionOptions
        {
            PageNumber = 1,
            PositionX = 50,
            PositionY = 150,
            Width = 250,
            Height = 35,
            FillColorHex = "#000000",
            OverlayText = "[DATO PROTEGIDO]"
        };

        // Act
        var redactedPdf = await editorService.ApplyRedactionAsync(doc, redactOpts);

        // Assert
        Assert.NotNull(redactedPdf);
        Assert.True(redactedPdf.Length > 0);
    }

    [Fact]
    public async Task BatchEdit_ShouldApplyMultipleInteractiveModifications()
    {
        // Arrange
        var editorService = new PdfEditorService();
        var doc = PdfTestHelper.CreateSamplePdf(2, "Doc Edicion Visual Multiple");

        var batchItems = new List<BatchEditItem>
        {
            new BatchEditItem
            {
                Type = "text",
                PageNumber = 1,
                X = 100,
                Y = 120,
                Width = 250,
                Height = 25,
                Text = "Texto Interactivo Word-style",
                FontSize = 18,
                ColorHex = "#10B981",
                IsBold = true
            },
            new BatchEditItem
            {
                Type = "redaction",
                PageNumber = 1,
                X = 50,
                Y = 200,
                Width = 200,
                Height = 30,
                ColorHex = "#000000",
                Text = "[CONFIDENCIAL]"
            },
            new BatchEditItem
            {
                Type = "highlight",
                PageNumber = 2,
                X = 60,
                Y = 150,
                Width = 180,
                Height = 20
            }
        };

        // Act
        var batchEditedPdf = await editorService.BatchEditAsync(doc, batchItems);

        // Assert
        Assert.NotNull(batchEditedPdf);
        Assert.True(batchEditedPdf.Length > doc.Length);
    }

    [Fact]
    public async Task ExtractTextBlocks_ShouldDetectTextWithBoundingBoxes()
    {
        // Arrange
        var editorService = new PdfEditorService();
        var doc = PdfTestHelper.CreateSamplePdf(1, "Texto Para Extraccion");

        // Act
        var blocks = await editorService.ExtractTextBlocksAsync(doc, 1);

        // Assert
        Assert.NotNull(blocks);
        Assert.NotEmpty(blocks);
        Assert.Contains(blocks, b => b.Text.Contains("Texto Para Extraccion") || b.Text.Contains("PDF de Prueba"));
        Assert.True(blocks[0].Width > 0);
        Assert.True(blocks[0].Height > 0);
    }

    [Fact]
    public async Task CompressPdf_ShouldProduceValidCompressedPdf()
    {
        // Arrange
        var compressionService = new PdfCompressionService();
        var doc = PdfTestHelper.CreateSamplePdf(3, "Documento Para Comprimir");

        // Act
        var result = await compressionService.CompressPdfAsync(doc, new CompressionOptions { Level = CompressionLevel.Medium });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.CompressedPdf);
        Assert.True(result.CompressedPdf.Length > 0);
        Assert.Equal(doc.Length, result.OriginalSizeBytes);
    }

    [Fact]
    public async Task AddPageNumbers_ShouldStampHeadersFooters()
    {
        // Arrange
        var pageNumberService = new PdfPageNumberService();
        var doc = PdfTestHelper.CreateSamplePdf(4, "Documento Con Foliado");

        var opts = new PageNumberOptions
        {
            Format = "Página {n} de {total}",
            Position = PageNumberPosition.BottomRight,
            StartPage = 1,
            StartingNumber = 1
        };

        // Act
        var numberedPdf = await pageNumberService.AddPageNumbersAsync(doc, opts);

        // Assert
        Assert.NotNull(numberedPdf);
        Assert.True(numberedPdf.Length > doc.Length);
    }

    [Fact]
    public async Task RedactPdf_ShouldApplyBlackoutAndReturnValidPdf()
    {
        // Arrange
        var redactionService = new PdfRedactionService(_inspectionService);
        var doc = PdfTestHelper.CreateSamplePdf(2, "Documento Confidencial Para Censura");

        var opts = new DeepRedactionOptions
        {
            Areas = new List<DeepRedactionArea>
            {
                new()
                {
                    PageNumber = 1,
                    X = 50,
                    Y = 50,
                    Width = 200,
                    Height = 40,
                    FillColorHex = "#000000",
                    OverlayText = "[REDACTED]",
                    TextColorHex = "#FFFFFF"
                }
            },
            PermanentFlatten = true
        };

        // Act
        var redactedPdf = await redactionService.RedactPdfAsync(doc, opts);

        // Assert
        Assert.NotNull(redactedPdf);
        Assert.True(redactedPdf.Length > 0);
        var meta = await _inspectionService.InspectMetadataAsync(redactedPdf);
        Assert.Equal(2, meta.PageCount);
    }

    [Fact]
    public async Task ComparePages_ShouldDetectDifferencesBetweenDocuments()
    {
        // Arrange
        var comparisonService = new PdfComparisonService();
        var docA = PdfTestHelper.CreateSamplePdf(1, "Versión 1 Original del Contrato");
        var docB = PdfTestHelper.CreateSamplePdf(1, "Versión 2 Modificada con Cláusulas Nuevas");

        var opts = new ComparisonOptions
        {
            PageIndex = 0,
            Dpi = 100,
            Sensitivity = 0.10
        };

        // Act
        var result = await comparisonService.ComparePagesAsync(docA, docB, opts);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasDifferences);
        Assert.True(result.PixelsChanged > 0);
        Assert.True(result.DifferencePercentage > 0);
        Assert.StartsWith("data:image/png;base64,", result.ImageBase64A);
        Assert.StartsWith("data:image/png;base64,", result.ImageBase64B);
        Assert.StartsWith("data:image/png;base64,", result.DiffImageBase64);
    }

    [Fact]
    public async Task ExtractStructuredContent_ShouldExtractWordsAndReadingTime()
    {
        // Arrange
        var extractor = new PdfStructuredExtractorService();
        var doc = PdfTestHelper.CreateSamplePdf(2, "Informe Financiero https://example.com/reporte");

        // Act
        var content = await extractor.ExtractStructuredContentAsync(doc);

        // Assert
        Assert.NotNull(content);
        Assert.Equal(2, content.TotalPages);
        Assert.True(content.TotalWords > 0);
        Assert.True(content.TotalCharacters > 0);
        Assert.NotEmpty(content.Pages);
        Assert.Contains("Página 1", content.FullMarkdownText);
    }

    [Fact]
    public async Task RepairPdf_WithGarbageHeader_ShouldSanitizeAndRecover()
    {
        // Arrange
        var repairService = new PdfRepairService(_inspectionService);
        var cleanPdf = PdfTestHelper.CreateSamplePdf(3, "Documento Sano");

        // Corrupt by prepending HTML error junk and stripping part of the header
        var junkBytes = System.Text.Encoding.ASCII.GetBytes("<!DOCTYPE html><html><body>Error 502 Bad Gateway</body></html>\n");
        var corrupted = new byte[junkBytes.Length + cleanPdf.Length];
        Buffer.BlockCopy(junkBytes, 0, corrupted, 0, junkBytes.Length);
        Buffer.BlockCopy(cleanPdf, 0, corrupted, junkBytes.Length, cleanPdf.Length);

        // Act
        var result = await repairService.RepairPdfAsync(corrupted);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, result.RecoveredPageCount);
        Assert.True(result.RepairedPdf.Length > 0);
        Assert.NotEmpty(result.DiagnosticMessage);

        var meta = await _inspectionService.InspectMetadataAsync(result.RepairedPdf);
        Assert.Equal(3, meta.PageCount);
    }

    [Fact]
    public async Task GenerateImposition_TwoUp_ShouldHalveSheetCount()
    {
        // Arrange
        var impositionService = new PdfImpositionService();
        var doc = PdfTestHelper.CreateSamplePdf(4, "Documento 4 Páginas");
        var options = new ImpositionOptions
        {
            Mode = ImpositionMode.TwoUp,
            TargetSheetSize = "A4",
            DrawBorders = true
        };

        // Act
        var imposedBytes = await impositionService.GenerateImpositionAsync(doc, options);

        // Assert
        Assert.NotNull(imposedBytes);
        Assert.True(imposedBytes.Length > 0);
        var meta = await _inspectionService.InspectMetadataAsync(imposedBytes);
        Assert.Equal(2, meta.PageCount); // 4 pages imposed 2-up = 2 sheets
    }

    [Fact]
    public async Task GenerateImposition_Booklet_ShouldGenerateMultipleOfFourBookletSheets()
    {
        // Arrange
        var impositionService = new PdfImpositionService();
        var doc = PdfTestHelper.CreateSamplePdf(6, "Folleto 6 Páginas");
        var options = new ImpositionOptions
        {
            Mode = ImpositionMode.Booklet,
            TargetSheetSize = "A4",
            DrawBorders = true
        };

        // Act
        var imposedBytes = await impositionService.GenerateImpositionAsync(doc, options);

        // Assert
        Assert.NotNull(imposedBytes);
        Assert.True(imposedBytes.Length > 0);
        var meta = await _inspectionService.InspectMetadataAsync(imposedBytes);
        // 6 pages padded to 8 booklet pages = 2 sheets printed front & back = 4 PDF pages
        Assert.Equal(4, meta.PageCount);
    }

    [Fact]
    public async Task FlattenForms_WithInteractiveAcroForm_ShouldFlattenAndSanitize()
    {
        // Arrange
        var formService = new PdfFormService();
        var formPdf = PdfTestHelper.CreateSamplePdfWithForm();

        // Act 1 - Inspect
        var inspected = await formService.InspectFormsAsync(formPdf);
        Assert.NotEmpty(inspected);
        Assert.Contains(inspected, f => f.Name == "NombreCliente");

        // Act 2 - Flatten
        var result = await formService.FlattenFormsAsync(formPdf);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FlattenedFieldsCount > 0);
        Assert.True(result.FlattenedPdf.Length > 0);

        // Act 3 - Re-inspect flattened PDF (should have 0 interactive fields)
        var postInspection = await formService.InspectFormsAsync(result.FlattenedPdf);
        Assert.Empty(postInspection);
    }

    [Fact]
    public async Task ProcessBatch_Watermark_ShouldProcessAllFilesAndGenerateZip()
    {
        // Arrange
        var batchService = new PdfBatchService(
            new PdfCompressionService(),
            _transformService,
            new PdfPageNumberService(),
            new PdfRepairService(_inspectionService),
            new PdfFormService());

        var file1 = PdfTestHelper.CreateSamplePdf(2, "Doc 1");
        var file2 = PdfTestHelper.CreateSamplePdf(3, "Doc 2");
        var file3 = PdfTestHelper.CreateSamplePdf(1, "Doc 3");

        var batchInput = new List<(string FileName, byte[] Bytes)>
        {
            ("factura_001.pdf", file1),
            ("factura_002.pdf", file2),
            ("contrato_003.pdf", file3)
        };

        var options = new BatchOperationOptions
        {
            Operation = BatchOperationType.Watermark,
            WatermarkText = "PROCESADO POR LOTE"
        };

        // Act
        var result = await batchService.ProcessBatchAsync(batchInput, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalFiles);
        Assert.Equal(3, result.SuccessfulFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.True(result.ZipArchiveBytes.Length > 0);

        // Verify ZIP contents
        using var zipMs = new MemoryStream(result.ZipArchiveBytes);
        using var zip = new System.IO.Compression.ZipArchive(zipMs, System.IO.Compression.ZipArchiveMode.Read);
        Assert.Equal(4, zip.Entries.Count); // 3 PDFs + 1 INFORME_LOTE.txt
        Assert.Contains(zip.Entries, e => e.Name == "INFORME_LOTE.txt");
        Assert.Contains(zip.Entries, e => e.Name.Contains("factura_001"));
    }

    [Fact]
    public async Task ProcessBatch_Compress_ShouldCompressAllFiles()
    {
        // Arrange
        var batchService = new PdfBatchService(
            new PdfCompressionService(),
            _transformService,
            new PdfPageNumberService(),
            new PdfRepairService(_inspectionService),
            new PdfFormService());

        var file1 = PdfTestHelper.CreateSamplePdf(3, "Comp 1");
        var file2 = PdfTestHelper.CreateSamplePdf(4, "Comp 2");

        var batchInput = new List<(string FileName, byte[] Bytes)>
        {
            ("doc_a.pdf", file1),
            ("doc_b.pdf", file2)
        };

        var options = new BatchOperationOptions
        {
            Operation = BatchOperationType.Compress,
            CompressionLevel = CompressionLevel.Medium
        };

        // Act
        var result = await batchService.ProcessBatchAsync(batchInput, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(2, result.SuccessfulFiles);
        Assert.True(result.Results.All(r => r.Success));
    }

    [Fact]
    public async Task ValidatePdfA_OnRegularPdf_ShouldDetectNonCompliance()
    {
        // Arrange
        var pdfAService = new PdfAService();
        var doc = PdfTestHelper.CreateSamplePdf(2, "Documento Estandar");

        // Act
        var result = await pdfAService.ValidatePdfAAsync(doc);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsCompliant);
        Assert.NotEmpty(result.ComplianceIssues);
        Assert.Equal("None", result.DetectedProfile);
    }

    [Fact]
    public async Task ConvertToPdfA_AndValidate_ShouldAchievePdfACompliance()
    {
        // Arrange
        var pdfAService = new PdfAService();
        var doc = PdfTestHelper.CreateSamplePdf(2, "Documento Judicial");

        // Act 1 - Convert to PDF/A-1b
        var convResult = await pdfAService.ConvertToPdfAAsync(doc, PdfAProfile.PdfA1b);

        // Assert 1
        Assert.NotNull(convResult);
        Assert.True(convResult.Success);
        Assert.True(convResult.ConvertedPdf.Length > 0);

        // Act 2 - Validate the converted PDF
        var valResult = await pdfAService.ValidatePdfAAsync(convResult.ConvertedPdf);

        // Assert 2
        Assert.NotNull(valResult);
        Assert.True(valResult.IsCompliant, string.Join(" | ", valResult.ComplianceIssues));
        Assert.Equal("PDF/A-1b", valResult.DetectedProfile);
        Assert.NotEmpty(valResult.ComplianceChecksPassed);
        Assert.Empty(valResult.ComplianceIssues);
    }

    [Fact]
    public async Task ExtractTables_FromPdfWithTable_ShouldExtractRowsAndCsv()
    {
        // Arrange
        var tableExtractor = new PdfTableExtractorService();
        var pdfWithTable = PdfTestHelper.CreateSamplePdfWithTable();

        // Act
        var result = await tableExtractor.ExtractTablesAsync(pdfWithTable, new TableExtractionOptions
        {
            Delimiter = ",",
            FirstRowIsHeader = true,
            MinColumnGap = 15.0
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalTables > 0);
        Assert.True(result.TotalRows > 0);
        Assert.NotEmpty(result.CombinedCsv);
        Assert.Contains("001", result.CombinedCsv);
        Assert.Contains("Licencia Software", result.CombinedCsv);
    }

    [Fact]
    public async Task ConvertOffice_FromCsv_ShouldProduceValidPdf()
    {
        // Arrange
        var officeConverter = new PdfOfficeConverterService();
        var csvContent = "ID,Cliente,Factura,Total\n1,Acme Corp,F-2026-01,5200 EUR\n2,GlobalTech,F-2026-02,12800 EUR";
        var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);

        // Act
        var result = await officeConverter.ConvertToPdfAsync("facturacion.csv", csvBytes, new OfficeConversionOptions
        {
            Title = "Listado de Facturación"
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.PdfBytes.Length > 0);
        Assert.Equal(OfficeDocumentType.Csv, result.DetectedType);
        Assert.True(result.PageCount >= 1);

        var meta = await _inspectionService.InspectMetadataAsync(result.PdfBytes);
        Assert.Equal("Listado de Facturación", meta.Title);
    }

    [Fact]
    public async Task ConvertOffice_FromDocx_ShouldProduceValidPdf()
    {
        // Arrange
        var officeConverter = new PdfOfficeConverterService();

        // Create a minimal synthetic docx (zip archive with word/document.xml)
        using var mem = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(mem, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:r><w:t>Primer párrafo del informe oficial</w:t></w:r></w:p>
    <w:p><w:r><w:t>Segundo párrafo con términos y condiciones legales.</w:t></w:r></w:p>
  </w:body>
</w:document>");
        }
        var docxBytes = mem.ToArray();

        // Act
        var result = await officeConverter.ConvertToPdfAsync("informe.docx", docxBytes);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.PdfBytes.Length > 0);
        Assert.Equal(OfficeDocumentType.Word, result.DetectedType);
        Assert.True(result.PageCount >= 1);
    }

    [Fact]
    public void HotFolder_StatusAndLifecycle_ShouldManageStateAndLogs()
    {
        // Arrange
        var pdfAService = new PdfAService();
        var compService = new PdfCompressionService();
        var repService = new PdfRepairService(_inspectionService);
        var formService = new PdfFormService();
        var offService = new PdfOfficeConverterService();

        using var hotFolder = new PdfHotFolderService(pdfAService, compService, repService, formService, offService);

        // Act 1: Initial status
        var initialStatus = hotFolder.GetStatus();
        Assert.False(initialStatus.IsActive);
        Assert.NotEmpty(initialStatus.RecentLogs);

        // Act 2: Configure & Start
        hotFolder.Configure(new HotFolderConfiguration
        {
            Enabled = true,
            Action = HotFolderAction.ConvertToPdfA,
            InputPath = Path.Combine(Path.GetTempPath(), "test_hotfolder_in_" + Guid.NewGuid().ToString("N")),
            ProcessedPath = Path.Combine(Path.GetTempPath(), "test_hotfolder_out_" + Guid.NewGuid().ToString("N")),
            FailedPath = Path.Combine(Path.GetTempPath(), "test_hotfolder_err_" + Guid.NewGuid().ToString("N"))
        });

        var activeStatus = hotFolder.GetStatus();
        Assert.True(activeStatus.IsActive);
        Assert.Equal(HotFolderAction.ConvertToPdfA, activeStatus.Configuration.Action);

        // Act 3: Pause
        hotFolder.Pause();
        var pausedStatus = hotFolder.GetStatus();
        Assert.False(pausedStatus.IsActive);
    }

    [Fact]
    public async Task InvoiceService_GenerateInvoice_ShouldProduceValidPdfAndFacturXXml()
    {
        // Arrange
        var invoiceService = new PdfInvoiceService();
        var req = new InvoiceGenerationRequest
        {
            InvoiceNumber = "FAC-2026-TEST-01",
            IssueDate = new DateTime(2026, 9, 25),
            DueDate = new DateTime(2026, 10, 25),
            Issuer = new InvoiceParty
            {
                Name = "Tecnologías Juan Manuel de la Vega S.L.",
                TaxId = "B-88776655",
                Address = "Paseo de la Castellana 100",
                City = "Madrid",
                PostalCode = "28046",
                Email = "contacto@empresa.es",
                Iban = "ES91 2100 0418 4502 0005 1332",
                Bic = "CAIXESBBXXX"
            },
            Customer = new InvoiceParty
            {
                Name = "Cliente Corporativo Global S.A.",
                TaxId = "A-11223344",
                Address = "Avenida Diagonal 200",
                City = "Barcelona",
                PostalCode = "08018",
                Email = "facturas@clienteglobal.com"
            },
            Items = new List<InvoiceItem>
            {
                new() { Description = "Licencia PDF Powerhouse Enterprise", Quantity = 2, UnitPrice = 1250m, TaxRatePercent = 21m },
                new() { Description = "Consultoría de Integración y Despliegue", Quantity = 10, UnitPrice = 90m, TaxRatePercent = 21m }
            },
            IrpfRatePercent = 15m,
            IncludeSepaQr = true,
            EmbedFacturXXml = true
        };

        // Act
        var result = await invoiceService.GenerateInvoiceAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.PdfBytes.Length > 0);
        Assert.Equal(3400m, result.Subtotal); // (2*1250) + (10*90) = 2500 + 900 = 3400
        Assert.Equal(714m, result.TotalTax); // 3400 * 0.21 = 714
        Assert.Equal(510m, result.TotalIrpf); // 3400 * 0.15 = 510
        Assert.Equal(3604m, result.TotalAmount); // 3400 + 714 - 510 = 3604
        Assert.True(result.FacturXEmbedded);
        Assert.True(result.QrCodeGenerated);
        Assert.Contains("CrossIndustryInvoice", result.ElectronicInvoiceXml);
        Assert.Contains("FAC-2026-TEST-01", result.ElectronicInvoiceXml);
        Assert.Contains("Tecnologías Juan Manuel de la Vega S.L.", result.ElectronicInvoiceXml);

        var renderedPng = await _inspectionService.RenderPageToPngAsync(result.PdfBytes, 0, 150);
        await File.WriteAllBytesAsync("invoice_rendered.png", renderedPng);
    }

    [Fact]
    public void InvoiceService_GenerateSepaPaymentQrPng_ShouldProduceValidPng()
    {
        // Arrange
        var invoiceService = new PdfInvoiceService();
        var issuer = new InvoiceParty
        {
            Name = "Empresa JDL",
            Iban = "ES91 2100 0418 4502 0005 1332",
            Bic = "CAIXESBBXXX"
        };

        // Act
        var qrPng = invoiceService.GenerateSepaPaymentQrPng(issuer, "FAC-001", 150.50m, "Pago Factura FAC-001");

        // Assert
        Assert.NotNull(qrPng);
        Assert.True(qrPng.Length > 100);
        // PNG magic header: 0x89, 'P', 'N', 'G'
        Assert.Equal(0x89, qrPng[0]);
        Assert.Equal((byte)'P', qrPng[1]);
        Assert.Equal((byte)'N', qrPng[2]);
        Assert.Equal((byte)'G', qrPng[3]);
    }

    [Fact]
    public async Task FormBuilder_CreateStandardRegistrationForm_ShouldProduceFillablePdf()
    {
        // Arrange
        var builderService = new PdfFormBuilderService();

        // Act
        var result = await builderService.CreateStandardRegistrationFormAsync("Juan Manuel Technologies", "Formulario de Solicitud Oficial");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.PdfBytes.Length > 0);
        Assert.True(result.FieldsCreated >= 8);
        Assert.Contains("nombre_completo", result.FieldNames);
        Assert.Contains("nif_cif", result.FieldNames);
        Assert.Contains("acepta_rgpd", result.FieldNames);
    }

    [Fact]
    public async Task FormBuilder_BuildCustomForm_ShouldGenerateAcroFormFields()
    {
        // Arrange
        var builderService = new PdfFormBuilderService();
        var req = new FormBuildRequest
        {
            DocumentTitle = "Cuestionario de Evaluación",
            Subtitle = "Documento de prueba interactivo",
            Fields = new List<FormFieldDefinition>
            {
                new() { PageNumber = 1, FieldName = "pregunta_1", Label = "¿Cuál es su valoración?", X = 50, Y = 120, Width = 300, Height = 25, DefaultValue = "Excelente" },
                new() { PageNumber = 1, FieldName = "confirmacion", Label = "Confirmo la veracidad", Type = FormFieldType.CheckBox, X = 50, Y = 160, Width = 20, Height = 20, DefaultValue = "true" }
            }
        };

        // Act
        var result = await builderService.BuildInteractiveFormAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, result.FieldsCreated);
        Assert.Equal(2, result.FieldNames.Count);
    }

    [Fact]
    public async Task Ocr_PerformOcrOnImage_ShouldProcessImage()
    {
        var ocrService = new PdfOcrService(new PdfInspectionService());
        var invoiceService = new PdfInvoiceService();
        var issuer = new InvoiceParty { Name = "Test", Iban = "ES1234567890", Bic = "TEST" };
        var qrPng = invoiceService.GenerateSepaPaymentQrPng(issuer, "FAC-001", 100m, "Test OCR");

        var result = await ocrService.PerformOcrOnImageAsync(qrPng);
        Assert.NotNull(result);
        Assert.True(result.ProcessingTimeMs >= 0);
        Assert.False(string.IsNullOrWhiteSpace(Tesseract.TesseractEnviornment.CustomSearchPath));
    }

    [Fact]
    public async Task Ocr_PerformOcrOnImage_WithNullOrEmptyLanguage_ShouldFallbackGracefully()
    {
        var ocrService = new PdfOcrService(new PdfInspectionService());
        var invoiceService = new PdfInvoiceService();
        var issuer = new InvoiceParty { Name = "Test", Iban = "ES1234567890", Bic = "TEST" };
        var qrPng = invoiceService.GenerateSepaPaymentQrPng(issuer, "FAC-002", 50m, "Test Fallback");

        var result = await ocrService.PerformOcrOnImageAsync(qrPng, new OcrOptions { Language = "" });
        Assert.NotNull(result);
    }
}

