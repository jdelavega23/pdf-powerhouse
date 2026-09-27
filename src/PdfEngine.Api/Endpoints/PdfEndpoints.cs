using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfEngine.Core.Services;

namespace PdfEngine.Api.Endpoints;

public static class PdfEndpoints
{
    public static void MapPdfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pdf")
                       .WithTags("PDF Operations");

        // Health Check
        app.MapGet("/api/health", () => Results.Ok(new
        {
            Status = "Online",
            Project = "PDF Powerhouse (PDF_JDL)",
            Architect = "Juan Manuel de la Vega",
            Engines = new[]
            {
                "Google PDFium (Native C++ via Docnet)",
                "PdfSharpCore (Advanced Page & Vector Transform)",
                "BouncyCastle (Cryptographic Security & Digital Signatures)"
            },
            Timestamp = DateTime.UtcNow
        })).WithTags("System");

        app.MapGet("/favicon.ico", () => Results.NoContent());

        // 1. Merge
        group.MapPost("/merge", async (
            IFormFileCollection files,
            [FromServices] IPdfMergeService mergeService,
            CancellationToken ct) =>
        {
            if (files == null || files.Count < 2)
            {
                return Results.BadRequest(new { error = "Se requieren al menos 2 archivos PDF para unir." });
            }

            var pdfList = new List<byte[]>(files.Count);
            foreach (var file in files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                pdfList.Add(ms.ToArray());
            }

            var mergedBytes = await mergeService.MergeAsync(pdfList, ct);
            return Results.File(mergedBytes, "application/pdf", "documentos_unidos.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Une múltiples documentos PDF en un solo archivo.");

        // 2. Split into ZIP
        group.MapPost("/split", async (
            IFormFile file,
            [FromServices] IPdfSplitService splitService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado o vacío." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var pages = await splitService.SplitAllPagesAsync(ms.ToArray(), ct);

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    var entry = archive.CreateEntry($"pagina_{i + 1:D3}.pdf", System.IO.Compression.CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pages[i], ct);
                }
            }

            zipStream.Position = 0;
            return Results.File(zipStream.ToArray(), "application/zip", "paginas_divididas.zip");
        })
        .DisableAntiforgery()
        .WithSummary("Divide un documento PDF en páginas individuales empaquetadas en un archivo ZIP.");

        // 3. Extract Pages
        group.MapPost("/extract-pages", async (
            IFormFile file,
            [FromForm] string pageRange,
            [FromServices] IPdfSplitService splitService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrWhiteSpace(pageRange))
                return Results.BadRequest(new { error = "Debe especificar el rango de páginas (ejemplo: '1-3, 5, 8')." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var extracted = await splitService.ExtractPagesAsync(ms.ToArray(), pageRange, ct);

            return Results.File(extracted, "application/pdf", "paginas_extraidas.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Extrae páginas específicas de un PDF según un rango (ej: '1-3, 5, 8').");

        // 4. Rotate
        group.MapPost("/rotate", async (
            IFormFile file,
            [FromForm] int degrees,
            [FromForm] string? targetPages,
            [FromServices] IPdfTransformService transformService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (degrees % 90 != 0)
                return Results.BadRequest(new { error = "Los grados de rotación deben ser múltiplos de 90 (90, 180, 270)." });

            List<int>? pages = null;
            if (!string.IsNullOrWhiteSpace(targetPages))
            {
                pages = PdfSplitService.ParsePageRanges(targetPages, int.MaxValue);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var rotated = await transformService.RotatePagesAsync(ms.ToArray(), degrees, pages, ct);

            return Results.File(rotated, "application/pdf", "documento_rotado.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Rota páginas de un PDF (90°, 180°, 270°).");

        // 5. Watermark
        group.MapPost("/watermark", async (
            IFormFile file,
            [FromForm] string text,
            [FromForm] double? opacity,
            [FromForm] double? fontSize,
            [FromForm] string? colorHex,
            [FromForm] double? rotationDegrees,
            [FromForm] bool? behindContent,
            [FromServices] IPdfTransformService transformService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { error = "El texto de la marca de agua no puede estar vacío." });

            var opts = new WatermarkOptions
            {
                Text = text,
                Opacity = opacity ?? 0.3,
                FontSize = fontSize ?? 48,
                FontColorHex = colorHex ?? "#808080",
                RotationDegrees = rotationDegrees ?? -45,
                BehindContent = behindContent ?? false
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var stamped = await transformService.ApplyWatermarkAsync(ms.ToArray(), opts, ct);

            return Results.File(stamped, "application/pdf", "documento_con_marca_de_agua.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Aplica una marca de agua de texto personalizada sobre las páginas.");

        // 6. Protect with Password & Permissions
        group.MapPost("/protect", async (
            IFormFile file,
            [FromForm] string userPassword,
            [FromForm] string? ownerPassword,
            [FromForm] bool? permitPrint,
            [FromForm] bool? permitCopy,
            [FromServices] IPdfSecurityService securityService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrEmpty(userPassword))
                return Results.BadRequest(new { error = "Se requiere una contraseña de usuario." });

            var opts = new PdfProtectionOptions
            {
                UserPassword = userPassword,
                OwnerPassword = ownerPassword ?? userPassword,
                PermitPrint = permitPrint ?? true,
                PermitCopyContent = permitCopy ?? true
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var protectedBytes = await securityService.ProtectPdfAsync(ms.ToArray(), opts, ct);

            return Results.File(protectedBytes, "application/pdf", "documento_protegido.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Protege el documento con cifrado y contraseñas de apertura y permisos.");

        // 7. Unlock
        group.MapPost("/unlock", async (
            IFormFile file,
            [FromForm] string password,
            [FromServices] IPdfSecurityService securityService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrEmpty(password))
                return Results.BadRequest(new { error = "Debe proporcionar la contraseña para desbloquear." });

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var unlocked = await securityService.UnlockPdfAsync(ms.ToArray(), password, ct);
                return Results.File(unlocked, "application/pdf", "documento_desbloqueado.pdf");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "No se pudo desbloquear el PDF. Contraseña incorrecta o formato no soportado.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Desbloquea y elimina la contraseña de un PDF protegido.");

        // 8. Inspect Metadata
        group.MapPost("/inspect", async (
            IFormFile file,
            [FromForm] string? password,
            [FromServices] IPdfInspectionService inspectionService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var metadata = await inspectionService.InspectMetadataAsync(ms.ToArray(), password, ct);

            return Results.Ok(metadata);
        })
        .DisableAntiforgery()
        .WithSummary("Inspecciona los metadatos completos, número de páginas, dimensiones y estado de cifrado.");

        // 9. Render Page using Google PDFium
        group.MapPost("/render-page", async (
            IFormFile file,
            [FromForm] int pageIndex,
            [FromForm] int? dpi,
            [FromForm] string? password,
            [FromServices] IPdfInspectionService inspectionService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var pngBytes = await inspectionService.RenderPageToPngAsync(ms.ToArray(), pageIndex, dpi ?? 150, password, ct);

            return Results.File(pngBytes, "image/png", $"pagina_{pageIndex + 1}.png");
        })
        .DisableAntiforgery()
        .WithSummary("Renderiza una página del PDF a imagen PNG de alta fidelidad usando el motor nativo Google PDFium.");

        // 10. Digital Sign (PAdES / PKCS#7)
        group.MapPost("/sign", async (
            IFormFile file,
            IFormFile? certificatePfx,
            [FromForm] string? certificatePassword,
            [FromForm] string? signerName,
            [FromForm] string? reason,
            [FromForm] string? location,
            [FromForm] int? targetPage,
            [FromForm] bool? includeVisualStamp,
            [FromServices] IPdfSigningService signingService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            byte[]? pfxBytes = null;
            if (certificatePfx != null && certificatePfx.Length > 0)
            {
                using var pfxMs = new MemoryStream();
                await certificatePfx.CopyToAsync(pfxMs, ct);
                pfxBytes = pfxMs.ToArray();
            }

            var options = new DigitalSignatureOptions
            {
                CertificatePfxBytes = pfxBytes,
                CertificatePassword = certificatePassword ?? string.Empty,
                SignerName = string.IsNullOrWhiteSpace(signerName) ? "Juan Manuel de la Vega" : signerName,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Aprobado y Conforme" : reason,
                Location = string.IsNullOrWhiteSpace(location) ? "España" : location,
                TargetPageNumber = targetPage ?? 1,
                IncludeVisualStamp = includeVisualStamp ?? true
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var signedBytes = await signingService.SignPdfAsync(ms.ToArray(), options, ct);

            return Results.File(signedBytes, "application/pdf", $"firmado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Firma digitalmente un documento PDF con certificación criptográfica y estampa visual PAdES.");

        // 11. Verify Signature
        group.MapPost("/verify-signature", async (
            IFormFile file,
            [FromServices] IPdfSigningService signingService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var result = await signingService.VerifySignatureAsync(ms.ToArray(), ct);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithSummary("Verifica la autenticidad, integridad y validez de las firmas digitales presentes en el PDF.");

        // 12. Generate Development Certificate
        app.MapPost("/api/pdf/generate-dev-certificate", async (
            [FromQuery] string? commonName,
            [FromQuery] string? password,
            [FromServices] IPdfSigningService signingService) =>
        {
            var cert = await signingService.GenerateDevelopmentCertificateAsync(
                commonName ?? "Juan Manuel de la Vega",
                password ?? "TestPassword123!");

            return Results.Ok(new
            {
                cert.Subject,
                cert.Password,
                cert.NotBefore,
                cert.NotAfter,
                cert.Thumbprint,
                PfxBase64 = Convert.ToBase64String(cert.PfxBytes),
                DownloadHint = "Puedes usar este certificado PFX para firmar documentos en la plataforma."
            });
        })
        .WithTags("Security")
        .WithSummary("Genera un certificado digital X.509 RSA-2048 válido para firmas digitales locales.");

        // 13. OCR (Optical Character Recognition)
        group.MapPost("/ocr", async (
            IFormFile file,
            [FromForm] string? language,
            [FromServices] IPdfOcrService ocrService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo de imagen o PDF no proporcionado." });

            var opts = new OcrOptions
            {
                Language = string.IsNullOrWhiteSpace(language) ? "spa+eng" : language
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            bool isPdf = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                         file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

            try
            {
                OcrResult result;
                if (isPdf)
                {
                    result = await ocrService.PerformOcrOnScannedPdfAsync(bytes, opts, ct);
                }
                else
                {
                    result = await ocrService.PerformOcrOnImageAsync(bytes, opts, ct);
                }

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                var fullMsg = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                if (ex.InnerException?.InnerException != null)
                {
                    fullMsg += $" -> {ex.InnerException.InnerException.Message}";
                }
                return Results.BadRequest(new { error = "Error durante el procesamiento OCR.", details = fullMsg });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Extrae texto reconocible mediante OCR (Tesseract 5) a partir de imágenes o PDFs escaneados.");

        // 14. Edit: Add Text
        group.MapPost("/edit/add-text", async (
            IFormFile file,
            [FromForm] string text,
            [FromForm] int? pageNumber,
            [FromForm] double? x,
            [FromForm] double? y,
            [FromForm] double? fontSize,
            [FromForm] string? fontColorHex,
            [FromForm] string? backgroundColorHex,
            [FromForm] bool? isBold,
            [FromForm] bool? isItalic,
            [FromServices] IPdfEditorService editorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { error = "El texto a insertar no puede estar vacío." });

            var opts = new TextAnnotationOptions
            {
                Text = text,
                PageNumber = pageNumber ?? 1,
                PositionX = x ?? 100,
                PositionY = y ?? 100,
                FontSize = fontSize ?? 14,
                FontColorHex = fontColorHex ?? "#000000",
                BackgroundColorHex = backgroundColorHex,
                IsBold = isBold ?? false,
                IsItalic = isItalic ?? false
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var editedBytes = await editorService.AddTextAnnotationAsync(ms.ToArray(), opts, ct);

            return Results.File(editedBytes, "application/pdf", $"editado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Inserta un nuevo bloque de texto con formato, posición y color en una página del PDF.");

        // 15. Edit: Add Image Stamp
        group.MapPost("/edit/add-image", async (
            IFormFile file,
            IFormFile image,
            [FromForm] int? pageNumber,
            [FromForm] double? x,
            [FromForm] double? y,
            [FromForm] double? width,
            [FromForm] double? height,
            [FromServices] IPdfEditorService editorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (image == null || image.Length == 0)
                return Results.BadRequest(new { error = "Archivo de imagen no proporcionado." });

            using var imgMs = new MemoryStream();
            await image.CopyToAsync(imgMs, ct);

            var opts = new ImageAnnotationOptions
            {
                PageNumber = pageNumber ?? 1,
                PositionX = x ?? 100,
                PositionY = y ?? 100,
                Width = width ?? 150,
                Height = height ?? 60,
                ImageBytes = imgMs.ToArray()
            };

            using var pdfMs = new MemoryStream();
            await file.CopyToAsync(pdfMs, ct);
            var editedBytes = await editorService.AddImageAnnotationAsync(pdfMs.ToArray(), opts, ct);

            return Results.File(editedBytes, "application/pdf", $"con_imagen_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Inserta una imagen, sello o firma manuscrita transparente en la posición indicada.");

        // 16. Edit: Redaction / Censorship
        group.MapPost("/edit/redact", async (
            IFormFile file,
            [FromForm] int? pageNumber,
            [FromForm] double? x,
            [FromForm] double? y,
            [FromForm] double? width,
            [FromForm] double? height,
            [FromForm] string? fillColorHex,
            [FromForm] string? overlayText,
            [FromServices] IPdfEditorService editorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            var opts = new RedactionOptions
            {
                PageNumber = pageNumber ?? 1,
                PositionX = x ?? 50,
                PositionY = y ?? 50,
                Width = width ?? 200,
                Height = height ?? 30,
                FillColorHex = fillColorHex ?? "#000000",
                OverlayText = overlayText ?? "[CENSURADO]"
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var redactedBytes = await editorService.ApplyRedactionAsync(ms.ToArray(), opts, ct);

            return Results.File(redactedBytes, "application/pdf", $"censurado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Aplica una caja de censura y redacción sobre una región confidencial de la página.");

        group.MapPost("/edit/batch", async (
            IFormFile file,
            [FromForm] string itemsJson,
            [FromServices] IPdfEditorService editorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            List<BatchEditItem>? items;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                items = JsonSerializer.Deserialize<List<BatchEditItem>>(itemsJson, options) ?? new List<BatchEditItem>();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Error al procesar los elementos de edición: {ex.Message}" });
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var editedBytes = await editorService.BatchEditAsync(ms.ToArray(), items, ct);

            return Results.File(editedBytes, "application/pdf", $"editado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Aplica un lote de modificaciones interactivas visuales (textos, dibujos, firmas, censuras, imágenes) al PDF.");

        group.MapPost("/extract-text-blocks", async (
            IFormFile file,
            [FromForm] int? pageNumber,
            [FromServices] IPdfEditorService editorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var blocks = await editorService.ExtractTextBlocksAsync(ms.ToArray(), pageNumber ?? 1, ct);

            return Results.Ok(blocks);
        })
        .DisableAntiforgery()
        .WithSummary("Extrae los bloques de texto existentes en una página con sus cajas delimitadoras (X, Y, Ancho, Alto), tamaño y color para su edición in situ.");

        // 16. Compress PDF
        group.MapPost("/compress", async (
            IFormFile file,
            [FromForm] PdfEngine.Core.Models.CompressionLevel? level,
            [FromServices] IPdfCompressionService compressionService,
            HttpResponse response,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            var opts = new CompressionOptions
            {
                Level = level ?? PdfEngine.Core.Models.CompressionLevel.Medium
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var result = await compressionService.CompressPdfAsync(ms.ToArray(), opts, ct);

            response.Headers.Append("X-Original-Size", result.OriginalSizeBytes.ToString());
            response.Headers.Append("X-Compressed-Size", result.CompressedSizeBytes.ToString());
            response.Headers.Append("X-Saved-Percentage", result.SavedPercentage.ToString("F1"));
            response.Headers.Append("Access-Control-Expose-Headers", "X-Original-Size, X-Compressed-Size, X-Saved-Percentage");

            return Results.File(result.CompressedPdf, "application/pdf", $"comprimido_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Comprime y optimiza un archivo PDF reduciendo su peso para envíos y trámites.");

        // 17. Convert: Images to PDF
        group.MapPost("/convert/images-to-pdf", async (
            IFormFileCollection images,
            [FromForm] ImageFitMode? fitMode,
            [FromForm] double? marginPt,
            [FromForm] bool? autoOrientation,
            [FromServices] IPdfConverterService converterService,
            CancellationToken ct) =>
        {
            if (images == null || images.Count == 0)
                return Results.BadRequest(new { error = "Debe proporcionar al menos una imagen." });

            var imgList = new List<(string, byte[])>(images.Count);
            foreach (var img in images)
            {
                using var ms = new MemoryStream();
                await img.CopyToAsync(ms, ct);
                imgList.Add((img.FileName, ms.ToArray()));
            }

            var opts = new ImagesToPdfOptions
            {
                FitMode = fitMode ?? ImageFitMode.FitPage,
                MarginPt = marginPt ?? 20,
                AutoOrientation = autoOrientation ?? true
            };

            var pdfBytes = await converterService.ImagesToPdfAsync(imgList, opts, ct);
            return Results.File(pdfBytes, "application/pdf", "imagenes_convertidas.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Convierte una o múltiples imágenes (JPG, PNG, WebP) en un único documento PDF profesional.");

        // 18. Convert: PDF to Images (ZIP)
        group.MapPost("/convert/pdf-to-images", async (
            IFormFile file,
            [FromForm] int? dpi,
            [FromServices] IPdfConverterService converterService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var zipBytes = await converterService.PdfToImagesZipAsync(ms.ToArray(), dpi ?? 150, ct);

            var baseName = Path.GetFileNameWithoutExtension(file.FileName);
            return Results.File(zipBytes, "application/zip", $"{baseName}_imagenes.zip");
        })
        .DisableAntiforgery()
        .WithSummary("Convierte todas las páginas de un PDF a imágenes PNG de alta resolución empaquetadas en un ZIP.");

        // 19. Page Numbers
        group.MapPost("/page-numbers", async (
            IFormFile file,
            [FromForm] string? format,
            [FromForm] PageNumberPosition? position,
            [FromForm] int? startPage,
            [FromForm] int? startingNumber,
            [FromForm] double? fontSize,
            [FromForm] string? fontColorHex,
            [FromForm] double? marginPt,
            [FromServices] IPdfPageNumberService pageNumberService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            var opts = new PageNumberOptions
            {
                Format = string.IsNullOrWhiteSpace(format) ? "Página {n} de {total}" : format,
                Position = position ?? PageNumberPosition.BottomRight,
                StartPage = startPage ?? 1,
                StartingNumber = startingNumber ?? 1,
                FontSize = fontSize ?? 10,
                FontColorHex = fontColorHex ?? "#555555",
                MarginPt = marginPt ?? 30
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var numberedBytes = await pageNumberService.AddPageNumbersAsync(ms.ToArray(), opts, ct);

            return Results.File(numberedBytes, "application/pdf", $"numerado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Inserta numeración y foliado personalizado ('Página X de Y') en las páginas del PDF.");

        // 20. Deep Legal Redaction
        group.MapPost("/redact", async (
            IFormFile file,
            [FromForm] string areasJson,
            [FromForm] bool? permanentFlatten,
            [FromServices] IPdfRedactionService redactionService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            if (string.IsNullOrWhiteSpace(areasJson))
                return Results.BadRequest(new { error = "Se debe proporcionar la lista de áreas a censurar en formato JSON." });

            List<DeepRedactionArea>? areas;
            try
            {
                areas = JsonSerializer.Deserialize<List<DeepRedactionArea>>(areasJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Formato JSON de áreas de censura no válido.", details = ex.Message });
            }

            if (areas == null || areas.Count == 0)
                return Results.BadRequest(new { error = "La lista de áreas a censurar está vacía." });

            var opts = new DeepRedactionOptions
            {
                Areas = areas,
                PermanentFlatten = permanentFlatten ?? true
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var redactedBytes = await redactionService.RedactPdfAsync(ms.ToArray(), opts, ct);

            return Results.File(redactedBytes, "application/pdf", $"censurado_{file.FileName}");
        })
        .DisableAntiforgery()
        .WithSummary("Aplica censura legal permanente e irreversible a las zonas especificadas del PDF.");

        // 21. Visual PDF Comparison & Diff
        group.MapPost("/compare", async (
            IFormFile fileA,
            IFormFile fileB,
            [FromForm] int? pageIndex,
            [FromForm] int? dpi,
            [FromForm] double? sensitivity,
            [FromServices] IPdfComparisonService comparisonService,
            CancellationToken ct) =>
        {
            if (fileA == null || fileA.Length == 0)
                return Results.BadRequest(new { error = "Debe proporcionar el Documento Original (A)." });

            if (fileB == null || fileB.Length == 0)
                return Results.BadRequest(new { error = "Debe proporcionar el Documento Modificado (B)." });

            var opts = new ComparisonOptions
            {
                PageIndex = pageIndex ?? 0,
                Dpi = dpi ?? 150,
                Sensitivity = sensitivity ?? 0.10
            };

            using var msA = new MemoryStream();
            await fileA.CopyToAsync(msA, ct);

            using var msB = new MemoryStream();
            await fileB.CopyToAsync(msB, ct);

            try
            {
                var result = await comparisonService.ComparePagesAsync(msA.ToArray(), msB.ToArray(), opts, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante la comparación visual.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Compara visualmente dos PDFs página a página generando un mapa Diff con diferencias en rojo y verde.");

        // 22. Structured Content Extraction & Analytics
        group.MapPost("/extract-content", async (
            IFormFile file,
            [FromServices] IPdfStructuredExtractorService extractorService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var content = await extractorService.ExtractStructuredContentAsync(ms.ToArray(), ct);
                return Results.Ok(content);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error al extraer el contenido estructurado del PDF.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Extrae el texto completo estructurado, métricas de lectura, páginas y URLs detectadas.");

        // 23. PDF Repair & Sanitize (Doctor de PDFs)
        group.MapPost("/repair", async (
            IFormFile file,
            [FromServices] IPdfRepairService repairService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado o vacío." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var repairResult = await repairService.RepairPdfAsync(ms.ToArray(), ct);
                return Results.Ok(new
                {
                    success = repairResult.Success,
                    recoveredPageCount = repairResult.RecoveredPageCount,
                    originalSizeBytes = repairResult.OriginalSizeBytes,
                    repairedSizeBytes = repairResult.RepairedSizeBytes,
                    diagnosticMessage = repairResult.DiagnosticMessage,
                    repairedPdfBase64 = repairResult.RepairedPdf.Length > 0 
                        ? $"data:application/pdf;base64,{Convert.ToBase64String(repairResult.RepairedPdf)}"
                        : null
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante el diagnóstico y reparación del PDF.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Diagnostica, sanea y repara archivos PDF dañados, corruptos o con tablas XRef rotas.");

        // 24. Page Imposition & Booklet Creation
        group.MapPost("/imposition", async (
            IFormFile file,
            [FromForm] string? mode,
            [FromForm] string? sheetSize,
            [FromForm] bool? drawBorders,
            [FromForm] double? marginPoints,
            [FromServices] IPdfImpositionService impositionService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado o vacío." });

            var impositionMode = (mode?.ToLowerInvariant()) switch
            {
                "fourup" or "4up" => ImpositionMode.FourUp,
                "booklet" or "folleto" => ImpositionMode.Booklet,
                _ => ImpositionMode.TwoUp
            };

            var options = new ImpositionOptions
            {
                Mode = impositionMode,
                TargetSheetSize = string.IsNullOrWhiteSpace(sheetSize) ? "A4" : sheetSize,
                DrawBorders = drawBorders ?? true,
                MarginPoints = marginPoints ?? 15.0
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var imposedPdf = await impositionService.GenerateImpositionAsync(ms.ToArray(), options, ct);
                string outName = $"imposicion_{impositionMode.ToString().ToLowerInvariant()}.pdf";
                return Results.File(imposedPdf, "application/pdf", outName);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante la imposición de páginas.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Realiza imposición de páginas en 2-Up, 4-Up o Cuadernillo/Folleto grapado plegable.");

        // 25. AcroForms Inspection
        group.MapPost("/forms/inspect", async (
            IFormFile file,
            [FromServices] IPdfFormService formService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var fields = await formService.InspectFormsAsync(ms.ToArray(), ct);
                return Results.Ok(fields);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error al inspeccionar los formularios AcroForms.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Inspecciona los campos interactivos de formulario (AcroForms) en el documento.");

        // 26. AcroForms Irreversible Flattening
        group.MapPost("/forms/flatten", async (
            IFormFile file,
            [FromServices] IPdfFormService formService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var result = await formService.FlattenFormsAsync(ms.ToArray(), ct);
                return Results.Ok(new
                {
                    flattenedFieldsCount = result.FlattenedFieldsCount,
                    inspectedFields = result.InspectedFields,
                    flattenedPdfBase64 = result.FlattenedPdf.Length > 0
                        ? $"data:application/pdf;base64,{Convert.ToBase64String(result.FlattenedPdf)}"
                        : null
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error al aplanar los formularios del PDF.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Aplana de manera irreversible todos los formularios y widgets interactivos en contenido estático.");

        // 27. Enterprise Batch Studio (Procesamiento por Lotes Masivo)
        group.MapPost("/batch", async (
            IFormFileCollection files,
            [FromForm] string? operation,
            [FromForm] string? watermarkText,
            [FromForm] string? watermarkColorHex,
            [FromForm] double? watermarkOpacity,
            [FromForm] string? compressionLevel,
            [FromForm] string? numberPosition,
            [FromForm] string? numberFormat,
            [FromForm] int? rotateDegrees,
            [FromQuery] bool? downloadZip,
            [FromServices] IPdfBatchService batchService,
            CancellationToken ct) =>
        {
            if (files == null || files.Count == 0)
            {
                return Results.BadRequest(new { error = "Debe proporcionar al menos un archivo PDF para el procesamiento por lotes." });
            }

            var opType = (operation?.ToLowerInvariant()) switch
            {
                "watermark" or "marca" => BatchOperationType.Watermark,
                "numbering" or "numerar" => BatchOperationType.Numbering,
                "repair" or "reparar" => BatchOperationType.Repair,
                "flatten" or "aplanar" => BatchOperationType.FlattenForms,
                "rotate" or "rotar" => BatchOperationType.Rotate,
                _ => BatchOperationType.Compress
            };

            var compLvl = (compressionLevel?.ToLowerInvariant()) switch
            {
                "low" or "baja" => PdfEngine.Core.Models.CompressionLevel.Low,
                "extreme" or "extrema" => PdfEngine.Core.Models.CompressionLevel.Extreme,
                _ => PdfEngine.Core.Models.CompressionLevel.Medium
            };

            var numPos = (numberPosition?.ToLowerInvariant()) switch
            {
                "bottomcenter" => PageNumberPosition.BottomCenter,
                "bottomleft" => PageNumberPosition.BottomLeft,
                "topright" => PageNumberPosition.TopRight,
                "topcenter" => PageNumberPosition.TopCenter,
                "topleft" => PageNumberPosition.TopLeft,
                _ => PageNumberPosition.BottomRight
            };

            var options = new BatchOperationOptions
            {
                Operation = opType,
                WatermarkText = watermarkText,
                WatermarkColorHex = watermarkColorHex,
                WatermarkOpacity = watermarkOpacity,
                CompressionLevel = compLvl,
                NumberPosition = numPos,
                NumberFormat = numberFormat,
                RotateDegrees = rotateDegrees ?? 90
            };

            var inputList = new List<(string FileName, byte[] Bytes)>(files.Count);
            foreach (var f in files)
            {
                using var ms = new MemoryStream();
                await f.CopyToAsync(ms, ct);
                inputList.Add((f.FileName, ms.ToArray()));
            }

            try
            {
                var batchResult = await batchService.ProcessBatchAsync(inputList, options, ct);

                if (downloadZip == true)
                {
                    string zipName = $"lote_procesado_{opType.ToString().ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                    return Results.File(batchResult.ZipArchiveBytes, "application/zip", zipName);
                }

                return Results.Ok(new
                {
                    totalFiles = batchResult.TotalFiles,
                    successfulFiles = batchResult.SuccessfulFiles,
                    failedFiles = batchResult.FailedFiles,
                    results = batchResult.Results.Select(r => new
                    {
                        fileName = r.FileName,
                        success = r.Success,
                        message = r.Message,
                        originalSizeBytes = r.OriginalSizeBytes,
                        processedSizeBytes = r.ProcessedSizeBytes
                    }),
                    zipBase64 = batchResult.ZipArchiveBytes.Length > 0
                        ? $"data:application/zip;base64,{Convert.ToBase64String(batchResult.ZipArchiveBytes)}"
                        : null
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante el procesamiento por lotes.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Procesa múltiples documentos PDF concurrentemente aplicando compresión, marcas de agua, numeración o saneamiento.");

        // 28. PDF/A Compliance Validator
        group.MapPost("/pdfa/validate", async (
            IFormFile file,
            [FromServices] IPdfAService pdfAService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var result = await pdfAService.ValidatePdfAAsync(ms.ToArray(), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante la validación de conformidad PDF/A.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Valida si un PDF cumple con la norma ISO 19005 para archivo a largo plazo en sedes judiciales.");

        // 29. PDF/A Compliance Converter
        group.MapPost("/pdfa/convert", async (
            IFormFile file,
            [FromForm] string? profile,
            [FromServices] IPdfAService pdfAService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            var targetProfile = profile?.ToLowerInvariant() == "pdfa2b" 
                ? PdfAProfile.PdfA2b 
                : PdfAProfile.PdfA1b;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            try
            {
                var result = await pdfAService.ConvertToPdfAAsync(ms.ToArray(), targetProfile, ct);
                return Results.Ok(new
                {
                    success = result.Success,
                    targetProfile = result.TargetProfile.ToString(),
                    diagnosticMessage = result.DiagnosticMessage,
                    convertedPdfBase64 = result.ConvertedPdf.Length > 0
                        ? $"data:application/pdf;base64,{Convert.ToBase64String(result.ConvertedPdf)}"
                        : null
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error durante la conversión a PDF/A.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Convierte e inyecta metadatos canónicos XMP y OutputIntents para cumplimiento ISO 19005 (PDF/A).");

        // 30. Table Extraction (JSON)
        group.MapPost("/tables/extract", async (
            IFormFile file,
            [FromForm] string? delimiter,
            [FromForm] bool? firstRowIsHeader,
            [FromForm] int? page,
            [FromServices] IPdfTableExtractorService tableExtractor,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var opts = new TableExtractionOptions
            {
                Delimiter = string.IsNullOrEmpty(delimiter) ? "," : delimiter,
                FirstRowIsHeader = firstRowIsHeader ?? true,
                TargetPage = page
            };

            try
            {
                var result = await tableExtractor.ExtractTablesAsync(ms.ToArray(), opts, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error extrayendo tablas del documento.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Detecta y extrae tablas tabulares de un PDF en formato estructurado JSON y CSV.");

        // 30b. Table Extraction (Direct CSV Download)
        group.MapPost("/tables/extract-csv", async (
            IFormFile file,
            [FromForm] string? delimiter,
            [FromServices] IPdfTableExtractorService tableExtractor,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo PDF no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var opts = new TableExtractionOptions
            {
                Delimiter = string.IsNullOrEmpty(delimiter) ? "," : delimiter
            };

            try
            {
                var csv = await tableExtractor.ExtractCsvAsync(ms.ToArray(), opts, ct);
                var csvBytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
                return Results.File(csvBytes, "text/csv; charset=utf-8", "tablas_extraidas.csv");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Error generando CSV de tablas.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Descarga directa de todas las tablas del PDF en archivo .CSV con codificación UTF-8.");

        // 31. Office Conversion to PDF
        group.MapPost("/office/convert", async (
            IFormFile file,
            [FromForm] string? title,
            [FromServices] IPdfOfficeConverterService officeConverter,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "Archivo Office/Texto no proporcionado." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var opts = new OfficeConversionOptions
            {
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title
            };

            try
            {
                var result = await officeConverter.ConvertToPdfAsync(file.FileName, ms.ToArray(), opts, ct);
                if (!result.Success)
                {
                    return Results.BadRequest(new { error = result.ErrorMessage ?? "Error en conversión de documento." });
                }

                return Results.Ok(new
                {
                    success = result.Success,
                    sourceFileName = result.SourceFileName,
                    detectedType = result.DetectedType.ToString(),
                    pageCount = result.PageCount,
                    engineUsed = result.EngineUsed,
                    pdfBase64 = $"data:application/pdf;base64,{Convert.ToBase64String(result.PdfBytes)}"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Fallo en motor de conversión Office.", details = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithSummary("Convierte archivos Office (Word .docx, Excel .xlsx/.csv, PowerPoint, Texto, RTF) a PDF de alta resolución.");

        // 31b. Office Engine Status
        group.MapGet("/office/status", async (
            [FromServices] IPdfOfficeConverterService officeConverter,
            CancellationToken ct) =>
        {
            bool libreOfficeAvailable = await officeConverter.IsLibreOfficeAvailableAsync(ct);
            return Results.Ok(new
            {
                isLibreOfficeAvailable = libreOfficeAvailable,
                activeEngine = libreOfficeAvailable ? "LibreOffice-Headless + Native-PdfEngine" : "Native-PdfEngine (C#)",
                supportedFormats = new[] { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".csv", ".tsv", ".txt", ".rtf", ".html" }
            });
        })
        .WithSummary("Consulta el estado de los motores de conversión Office (LibreOffice / C# nativo).");

        // 32. Hot Folders Watchdog API
        group.MapGet("/hotfolder/status", (
            [FromServices] IPdfHotFolderService hotFolderService) =>
        {
            return Results.Ok(hotFolderService.GetStatus());
        })
        .WithSummary("Devuelve el estado en tiempo real del Watchdog de Carpetas Calientes y sus registros recientes.");

        group.MapPost("/hotfolder/configure", (
            [FromBody] HotFolderConfiguration config,
            [FromServices] IPdfHotFolderService hotFolderService) =>
        {
            hotFolderService.Configure(config);
            return Results.Ok(hotFolderService.GetStatus());
        })
        .DisableAntiforgery()
        .WithSummary("Actualiza la configuración, carpetas y acción automática del Watchdog.");

        group.MapPost("/hotfolder/start", (
            [FromServices] IPdfHotFolderService hotFolderService) =>
        {
            hotFolderService.Start();
            return Results.Ok(hotFolderService.GetStatus());
        })
        .DisableAntiforgery()
        .WithSummary("Inicia la vigilancia automática de carpetas calientes.");

        group.MapPost("/hotfolder/pause", (
            [FromServices] IPdfHotFolderService hotFolderService) =>
        {
            hotFolderService.Pause();
            return Results.Ok(hotFolderService.GetStatus());
        })
        .DisableAntiforgery()
        .WithSummary("Pone en pausa el escaneo del Watchdog.");

        group.MapPost("/hotfolder/scan", async (
            [FromServices] IPdfHotFolderService hotFolderService,
            CancellationToken ct) =>
        {
            int processed = await hotFolderService.TriggerScanAsync(ct);
            return Results.Ok(new
            {
                message = $"Escaneo completado. Se procesaron {processed} archivo(s).",
                status = hotFolderService.GetStatus()
            });
        })
        .DisableAntiforgery()
        .WithSummary("Dispara un escaneo manual inmediato de la carpeta caliente de entrada.");

        // 25. Generador de Facturas & Factura Electrónica (Factur-X / ZUGFeRD)
        group.MapPost("/invoice/generate", async (
            [FromBody] InvoiceGenerationRequest request,
            [FromServices] IPdfInvoiceService invoiceService,
            CancellationToken ct) =>
        {
            var result = await invoiceService.GenerateInvoiceAsync(request, ct);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Message });
            }

            return Results.File(result.PdfBytes, "application/pdf", $"Factura_{result.InvoiceNumber}.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Genera una factura ejecutiva vectorial con código QR SEPA y Factur-X XML embebido.");

        group.MapPost("/invoice/details", async (
            [FromBody] InvoiceGenerationRequest request,
            [FromServices] IPdfInvoiceService invoiceService,
            CancellationToken ct) =>
        {
            var result = await invoiceService.GenerateInvoiceAsync(request, ct);
            return Results.Ok(new
            {
                result.Success,
                result.InvoiceNumber,
                result.Subtotal,
                result.TotalTax,
                result.TotalIrpf,
                result.TotalAmount,
                result.FacturXEmbedded,
                result.QrCodeGenerated,
                result.TaxBreakdown,
                result.Message,
                ElectronicInvoiceXml = result.ElectronicInvoiceXml,
                PdfBase64 = Convert.ToBase64String(result.PdfBytes)
            });
        })
        .DisableAntiforgery()
        .WithSummary("Calcula totales, genera XML Factur-X y devuelve factura con base64 para vista previa.");

        group.MapPost("/invoice/xml", (
            [FromBody] InvoiceGenerationRequest request,
            [FromServices] IPdfInvoiceService invoiceService) =>
        {
            string xml = invoiceService.GenerateFacturXXml(request);
            return Results.Content(xml, "application/xml; charset=utf-8");
        })
        .DisableAntiforgery()
        .WithSummary("Genera únicamente el archivo XML estándar Factur-X / ZUGFeRD EN 16931.");

        // 26. Creador de Formularios Rellenables Interactivos (AcroForms)
        group.MapPost("/forms/build", async (
            [FromBody] FormBuildRequest request,
            [FromServices] IPdfFormBuilderService formBuilderService,
            CancellationToken ct) =>
        {
            var result = await formBuilderService.BuildInteractiveFormAsync(request, ct);
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Message });
            }

            return Results.File(result.PdfBytes, "application/pdf", "formulario_rellenable.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Crea o añade campos interactivos rellenables (cajas de texto, casillas, firma) a un PDF.");

        group.MapPost("/forms/template/registration", async (
            [FromQuery] string? company,
            [FromQuery] string? title,
            [FromServices] IPdfFormBuilderService formBuilderService,
            CancellationToken ct) =>
        {
            var result = await formBuilderService.CreateStandardRegistrationFormAsync(
                company ?? "Tecnologías Juan Manuel de la Vega",
                title ?? "Formulario Oficial de Solicitud y Registro",
                ct);

            return Results.File(result.PdfBytes, "application/pdf", "formulario_registro_oficial.pdf");
        })
        .DisableAntiforgery()
        .WithSummary("Genera una plantilla ejecutiva oficial de registro con campos rellenables y casilla RGPD.");
    }
}

