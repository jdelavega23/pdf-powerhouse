using System.Text;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfEngine.Core.Services;

public sealed class PdfTableExtractorService : IPdfTableExtractorService
{
    public Task<TableExtractionResult> ExtractTablesAsync(byte[] pdfDocument, TableExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        var opts = options ?? new TableExtractionOptions();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfDocument);
            var tables = new List<PdfTable>();
            var combinedCsv = new StringBuilder();
            int totalRows = 0;
            int tableCounter = 1;

            foreach (var page in doc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (opts.TargetPage.HasValue && page.Number != opts.TargetPage.Value)
                {
                    continue;
                }

                var words = page.GetWords().ToList();
                if (words.Count == 0)
                {
                    continue;
                }

                var pageRows = ClusterWordsIntoRows(words, opts.MinColumnGap);
                if (pageRows.Count == 0)
                {
                    continue;
                }

                // Group contiguous rows that have 2 or more columns (tabular structure)
                var currentTableRows = new List<List<string>>();

                void FlushTable()
                {
                    if (currentTableRows.Count >= 2)
                    {
                        var headers = opts.FirstRowIsHeader && currentTableRows.Count > 0
                            ? currentTableRows[0]
                            : currentTableRows[0].Select((_, idx) => $"Col_{idx + 1}").ToList();

                        var dataRows = opts.FirstRowIsHeader && currentTableRows.Count > 1
                            ? currentTableRows.Skip(1).ToList()
                            : currentTableRows;

                        var formattedRows = dataRows.Select((cells, rowIdx) => new PdfTableRow
                        {
                            RowIndex = rowIdx + 1,
                            Cells = cells
                        }).ToList();

                        var tableCsv = BuildCsv(headers, formattedRows, opts.Delimiter);

                        tables.Add(new PdfTable
                        {
                            PageNumber = page.Number,
                            TableIndex = tableCounter++,
                            Headers = headers,
                            Rows = formattedRows,
                            Csv = tableCsv
                        });

                        totalRows += formattedRows.Count;

                        if (combinedCsv.Length > 0)
                        {
                            combinedCsv.AppendLine();
                        }
                        combinedCsv.AppendLine($"# Tabla {tableCounter - 1} (Pagina {page.Number})");
                        combinedCsv.Append(tableCsv);
                    }
                    currentTableRows.Clear();
                }

                foreach (var rowCells in pageRows)
                {
                    if (rowCells.Count >= 2)
                    {
                        currentTableRows.Add(rowCells);
                    }
                    else
                    {
                        // Single column break or text paragraph
                        if (currentTableRows.Count >= 2)
                        {
                            FlushTable();
                        }
                        else
                        {
                            currentTableRows.Clear();
                        }
                    }
                }

                FlushTable();
            }

            return new TableExtractionResult
            {
                TotalTables = tables.Count,
                TotalRows = totalRows,
                Tables = tables,
                CombinedCsv = combinedCsv.ToString()
            };
        }, cancellationToken);
    }

    public async Task<string> ExtractCsvAsync(byte[] pdfDocument, TableExtractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await ExtractTablesAsync(pdfDocument, options, cancellationToken);
        return result.CombinedCsv;
    }

    private static List<List<string>> ClusterWordsIntoRows(IReadOnlyList<Word> words, double minColumnGap)
    {
        // 1. Sort words top to bottom (descending Y), then left to right (ascending X)
        var sortedWords = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        // 2. Cluster into lines based on vertical baseline proximity (within 4 points)
        var lines = new List<List<Word>>();
        List<Word>? currentLine = null;
        double currentY = double.NaN;

        foreach (var word in sortedWords)
        {
            if (currentLine == null || Math.Abs(word.BoundingBox.Bottom - currentY) > 5.0)
            {
                currentLine = new List<Word> { word };
                lines.Add(currentLine);
                currentY = word.BoundingBox.Bottom;
            }
            else
            {
                currentLine.Add(word);
            }
        }

        // 3. For each line, sort words left-to-right and split into columns based on minColumnGap
        var tableRows = new List<List<string>>();

        foreach (var line in lines)
        {
            var orderedLine = line.OrderBy(w => w.BoundingBox.Left).ToList();
            var cells = new List<string>();
            var currentCellWords = new List<string>();
            Word? prevWord = null;

            foreach (var word in orderedLine)
            {
                if (prevWord != null)
                {
                    double gap = word.BoundingBox.Left - prevWord.BoundingBox.Right;
                    if (gap >= minColumnGap)
                    {
                        // New column detected
                        if (currentCellWords.Count > 0)
                        {
                            cells.Add(string.Join(" ", currentCellWords).Trim());
                            currentCellWords.Clear();
                        }
                    }
                }

                currentCellWords.Add(word.Text);
                prevWord = word;
            }

            if (currentCellWords.Count > 0)
            {
                cells.Add(string.Join(" ", currentCellWords).Trim());
            }

            if (cells.Count > 0)
            {
                tableRows.Add(cells);
            }
        }

        return tableRows;
    }

    private static string BuildCsv(IReadOnlyList<string> headers, IReadOnlyList<PdfTableRow> rows, string delimiter)
    {
        var sb = new StringBuilder();

        if (headers.Count > 0)
        {
            sb.AppendLine(string.Join(delimiter, headers.Select(EscapeCsvField)));
        }

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(delimiter, row.Cells.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        if (field.Contains(',') || field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
