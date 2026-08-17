using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DuplicateHunter.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace DuplicateHunter.Services;

public class ReportExportService : IReportExportService
{
    public async Task ExportToCsvAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("Group Hash,File Name,File Path,Extension,Size (Bytes),Formatted Size");

        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                sb.AppendLine($"\"{EscapeCsv(group.Hash)}\",\"{EscapeCsv(file.FileName)}\",\"{EscapeCsv(file.FilePath)}\",\"{EscapeCsv(file.Extension)}\",{file.Size},\"{EscapeCsv(group.FormattedSize)}\"");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportToJsonAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var reportData = new
        {
            ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Statistics = new
            {
                stats.FilesScanned,
                stats.DuplicateGroups,
                stats.WastedBytes,
                WastedSpaceFormatted = stats.WastedSpace
            },
            DuplicateGroups = groups.Select(g => new
            {
                g.Hash,
                g.ShortHash,
                g.FileCount,
                g.TotalSize,
                g.FormattedSize,
                Files = g.Files.Select(f => new
                {
                    f.FileName,
                    f.FilePath,
                    f.Extension,
                    f.Size,
                    f.IsSelected
                })
            })
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(reportData, options);

        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public async Task ExportToExcelAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Duplicate Hunter Report";
        summarySheet.Range(1, 1, 1, 2).Merge();
        summarySheet.Row(1).Style.Font.Bold = true;
        summarySheet.Row(1).Style.Font.FontSize = 16;

        summarySheet.Cell(3, 1).Value = "Generated";
        summarySheet.Cell(3, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        summarySheet.Cell(4, 1).Value = "Files Scanned";
        summarySheet.Cell(4, 2).Value = stats.FilesScanned;
        summarySheet.Cell(5, 1).Value = "Duplicate Groups";
        summarySheet.Cell(5, 2).Value = stats.DuplicateGroups;
        summarySheet.Cell(6, 1).Value = "Wasted Bytes";
        summarySheet.Cell(6, 2).Value = stats.WastedBytes;
        summarySheet.Cell(7, 1).Value = "Wasted Space";
        summarySheet.Cell(7, 2).Value = stats.WastedSpace;
        summarySheet.Columns(1, 2).AdjustToContents();

        var detailsSheet = workbook.Worksheets.Add("Duplicate Groups");
        detailsSheet.Cell(1, 1).Value = "Group Hash";
        detailsSheet.Cell(1, 2).Value = "Short Hash";
        detailsSheet.Cell(1, 3).Value = "File Name";
        detailsSheet.Cell(1, 4).Value = "File Path";
        detailsSheet.Cell(1, 5).Value = "Extension";
        detailsSheet.Cell(1, 6).Value = "Size (Bytes)";
        detailsSheet.Cell(1, 7).Value = "Group File Count";
        detailsSheet.Cell(1, 8).Value = "Group Total Size";
        detailsSheet.Range(1, 1, 1, 8).Style.Font.Bold = true;
        detailsSheet.Range(1, 1, 1, 8).Style.Fill.BackgroundColor = XLColor.LightGray;

        int row = 2;
        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                detailsSheet.Cell(row, 1).Value = group.Hash;
                detailsSheet.Cell(row, 2).Value = group.ShortHash;
                detailsSheet.Cell(row, 3).Value = file.FileName;
                detailsSheet.Cell(row, 4).Value = file.FilePath;
                detailsSheet.Cell(row, 5).Value = file.Extension;
                detailsSheet.Cell(row, 6).Value = file.Size;
                detailsSheet.Cell(row, 7).Value = group.FileCount;
                detailsSheet.Cell(row, 8).Value = group.TotalSize;
                row++;
            }
        }

        detailsSheet.SheetView.FreezeRows(1);
        detailsSheet.Columns().AdjustToContents();
        detailsSheet.Column(4).Width = Math.Min(Math.Max(detailsSheet.Column(4).Width, 40), 80);

        workbook.SaveAs(filePath);

        await Task.CompletedTask;
    }

    public async Task ExportToPdfAsync(string filePath, IEnumerable<DuplicateGroup> groups, ScanStatistics stats)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var pdfDocument = new PdfDocument();
        pdfDocument.Info.Title = "Duplicate Hunter Report";

        var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
        var headingFont = new XFont("Arial", 12, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 9, XFontStyle.Regular);
        var smallFont = new XFont("Arial", 8, XFontStyle.Regular);

        PdfPage page = AddPdfPage(pdfDocument);
        XGraphics gfx = XGraphics.FromPdfPage(page);

        double leftMargin = 40;
        double topMargin = 40;
        double width = page.Width.Point - leftMargin * 2;
        double y = topMargin;

        void WriteLine(string text, XFont font, double lineHeight = 14)
        {
            EnsurePdfSpace(ref page, ref gfx, ref y, lineHeight, pdfDocument, headingFont);
            gfx.DrawString(text, font, XBrushes.Black, new XPoint(leftMargin, y));
            y += lineHeight;
        }

        void WriteWrapped(string text, XFont font, double lineHeight = 12)
        {
            foreach (var line in WrapText(gfx, text, font, width))
            {
                WriteLine(line, font, lineHeight);
            }
        }

        gfx.DrawString("Duplicate Hunter Report", titleFont, XBrushes.Black, new XPoint(leftMargin, y));
        y += 26;
        WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", bodyFont);
        WriteLine($"Files Scanned: {stats.FilesScanned}", bodyFont);
        WriteLine($"Duplicate Groups: {stats.DuplicateGroups}", bodyFont);
        WriteLine($"Wasted Bytes: {stats.WastedBytes:N0}", bodyFont);
        WriteLine($"Wasted Space: {stats.WastedSpace}", bodyFont);
        y += 8;

        foreach (var group in groups)
        {
            EnsurePdfSpace(ref page, ref gfx, ref y, 56, pdfDocument, headingFont);
            gfx.DrawString($"Group: {group.ShortHash}", headingFont, XBrushes.Black, new XPoint(leftMargin, y));
            y += 16;
            WriteLine($"Hash: {group.Hash}", smallFont);
            WriteLine($"Files: {group.FileCount} | Total Size: {group.FormattedSize}", smallFont);
            y += 4;

            foreach (var file in group.Files)
            {
                EnsurePdfSpace(ref page, ref gfx, ref y, 36, pdfDocument, headingFont);
                WriteLine($"- {file.FileName} ({file.Size:N0} bytes)", bodyFont);
                WriteWrapped($"Path: {file.FilePath}", smallFont);
            }

            y += 8;
        }

        using var stream = File.Create(filePath);
        pdfDocument.Save(stream, false);

        await Task.CompletedTask;
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        return field.Replace("\"", "\"\"");
    }

    private static PdfPage AddPdfPage(PdfDocument document)
    {
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        return page;
    }

    private static void EnsurePdfSpace(ref PdfPage page, ref XGraphics gfx, ref double y, double requiredHeight, PdfDocument document, XFont headingFont)
    {
        double bottomMargin = 40;
        if (y + requiredHeight <= page.Height.Point - bottomMargin)
            return;

        page = AddPdfPage(document);
        gfx.Dispose();
        gfx = XGraphics.FromPdfPage(page);
        y = 40;
        gfx.DrawString("Duplicate Hunter Report (continued)", headingFont, XBrushes.Black, new XPoint(40, y));
        y += 20;
    }

    private static IEnumerable<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return string.Empty;
            yield break;
        }

        var words = text.Split(' ');
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                currentLine.Clear();
                currentLine.Append(candidate);
                continue;
            }

            if (currentLine.Length > 0)
            {
                yield return currentLine.ToString();
                currentLine.Clear();
            }

            if (gfx.MeasureString(word, font).Width <= maxWidth)
            {
                currentLine.Append(word);
                continue;
            }

            var chunk = new StringBuilder();
            foreach (char character in word)
            {
                var chunkCandidate = chunk + character.ToString();
                if (gfx.MeasureString(chunkCandidate, font).Width <= maxWidth)
                {
                    chunk.Append(character);
                }
                else
                {
                    if (chunk.Length > 0)
                    {
                        yield return chunk.ToString();
                    }

                    chunk.Clear();
                    chunk.Append(character);
                }
            }

            if (chunk.Length > 0)
            {
                currentLine.Append(chunk);
            }
        }

        if (currentLine.Length > 0)
        {
            yield return currentLine.ToString();
        }
    }
}
