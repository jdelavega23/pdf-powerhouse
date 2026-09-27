using PdfEngine.Api.Endpoints;
using PdfEngine.Core;

var baseDir = AppContext.BaseDirectory;
var candidateWwwroot = Path.Combine(baseDir, "wwwroot");

var webAppOptions = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = baseDir,
    WebRootPath = Directory.Exists(candidateWwwroot) ? candidateWwwroot : null
};

var builder = WebApplication.CreateBuilder(webAppOptions);

// Register Core PDF Engine (PDFium + PdfSharpCore + BouncyCastle)
builder.Services.AddPdfEngine();

// Add CORS for Web/SPA frontends
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PDF Powerhouse API (PDF_JDL)",
        Version = "v1.0",
        Description = "Motor de procesamiento de PDF de alto rendimiento impulsado por Google PDFium, PdfSharp y BouncyCastle.\nArquitectura diseñada para Juan Manuel de la Vega."
    });
});

var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (!isContainer && string.IsNullOrWhiteSpace(urlsEnv) && !args.Any(a => a.StartsWith("--urls=")))
{
    // Standalone desktop mode: default to port 5091 to avoid collision with Docker container (5090)
    builder.WebHost.UseUrls("http://localhost:5091");
}

var app = builder.Build();

app.UseCors();

// Enable Swagger UI (for developers/API inspection)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PDF Powerhouse API v1");
    c.RoutePrefix = "swagger";
});

// Serve embedded UI from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Direct bulletproof serving of index.html for root "/" and "/index.html"
async Task ServeIndexHtml(HttpContext context)
{
    var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
    if (!File.Exists(indexPath))
    {
        indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    }
    if (!File.Exists(indexPath))
    {
        indexPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
    }

    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.Redirect("/swagger");
    }
}

app.MapGet("/", ServeIndexHtml);
app.MapGet("/index.html", ServeIndexHtml);

// Map PDF Endpoints
app.MapPdfEndpoints();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var port = isContainer ? "5090" : "5091";
    Console.WriteLine("==================================================================");
    Console.WriteLine("  🚀 PDF POWERHOUSE (PDF_JDL) - ARQUITECTURA PRIVADA");
    Console.WriteLine("  👤 Diseñado e impulsado por Juan Manuel de la Vega");
    Console.WriteLine($"  🌐 Panel Web Interactivo : http://localhost:{port}/index.html");
    Console.WriteLine($"  📖 Documentación Swagger  : http://localhost:{port}/swagger");
    Console.WriteLine("==================================================================");

    // Auto-open browser on Windows desktop directly to index.html
    if (OperatingSystem.IsWindows() && !isContainer)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"http://localhost:{port}/index.html",
                    UseShellExecute = true
                });
            }
            catch { }
        });
    }
});

app.Run();
