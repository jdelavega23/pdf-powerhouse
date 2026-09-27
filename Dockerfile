# Stage 1: Build SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution and project files first for optimal docker caching
COPY PdfJdl.slnx .
COPY src/PdfEngine.Core/PdfEngine.Core.csproj src/PdfEngine.Core/
COPY src/PdfEngine.Api/PdfEngine.Api.csproj src/PdfEngine.Api/
COPY tests/PdfEngine.Tests/PdfEngine.Tests.csproj tests/PdfEngine.Tests/

# Restore dependencies
RUN dotnet restore src/PdfEngine.Api/PdfEngine.Api.csproj

# Copy source code and build
COPY src/ src/
RUN dotnet publish src/PdfEngine.Api/PdfEngine.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install native dependencies for Google PDFium, Tesseract 5 OCR and LibreOffice Headless
RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 \
    libfontconfig1 \
    libfreetype6 \
    tesseract-ocr \
    tesseract-ocr-spa \
    tesseract-ocr-eng \
    libleptonica-dev \
    libtesseract-dev \
    libc6-dev \
    libreoffice-writer-nogui \
    libreoffice-calc-nogui \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create symlinks for Charlesw Tesseract .NET wrapper (loads libleptonica-1.82.0.so, libtesseract50.so, and libdl)
RUN ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract50.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/libleptonica-1.82.0.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/libtesseract50.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/libdl.so && \
    ldconfig

# Copy compiled application
COPY --from=build /app/publish .

# Ensure x64 and tessdata directories contain symlinks and preinstalled language models
RUN mkdir -p /app/x64 /app/tessdata && \
    ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/libleptonica-1.82.0.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/libtesseract50.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /app/libdl.so && \
    (cp /usr/share/tesseract-ocr/5/tessdata/*.traineddata /app/tessdata/ 2>/dev/null || \
     cp /usr/share/tesseract-ocr/4.00/tessdata/*.traineddata /app/tessdata/ 2>/dev/null || \
     cp /usr/share/tessdata/*.traineddata /app/tessdata/ 2>/dev/null || true)

# Set environment
ENV ASPNETCORE_URLS=http://+:5090
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TESSDATA_PREFIX=/app/tessdata

EXPOSE 5090

ENTRYPOINT ["dotnet", "PdfEngine.Api.dll"]
