using ClosedXML.Excel;
using Mainframe.Models;

namespace Mainframe.Services;

public class ExcelExportService
{
    public void Export(
        string filePath,
        string userName,
        DateOnly startDate,
        DateOnly endDate,
        List<DailySummary> dailySummaries,
        List<ChargeCodeSummary> chargeCodeSummaries,
        List<ProjectSummary> projectSummaries,
        List<ChargeCodeSummary> allTimeChargeCodeSummaries,
        List<ProjectSummary> allTimeProjectSummaries,
        decimal allTimeTotalHours,
        DateOnly? oldestDate)
    {
        using var workbook = new XLWorkbook();

        BuildTimeEntriesSheet(workbook, userName, startDate, endDate, dailySummaries, chargeCodeSummaries, projectSummaries);
        BuildOverviewSheet(workbook, userName, allTimeChargeCodeSummaries, allTimeProjectSummaries, allTimeTotalHours, oldestDate);

        workbook.SaveAs(filePath);
    }

    private static void BuildTimeEntriesSheet(
        XLWorkbook workbook,
        string userName,
        DateOnly startDate,
        DateOnly endDate,
        List<DailySummary> dailySummaries,
        List<ChargeCodeSummary> chargeCodeSummaries,
        List<ProjectSummary> projectSummaries)
    {
        var ws = workbook.Worksheets.Add("Time Entries");

        // title
        ws.Cell(1, 1).Value = $"Time Entries: {startDate:MMM d, yyyy} - {endDate:MMM d, yyyy}";
        ws.Range(1, 1, 1, 7).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        // your name
        int headerRow = 2;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            ws.Cell(2, 1).Value = $"Name: {userName}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 12;
            headerRow = 3;
        }

        // headers
        int headersRow = headerRow + 1;
        var headers = new[] { "Date", "Charge Code", "Project", "Task", "Subtask", "Hours", "Notes" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headersRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // rows by day
        int row = headersRow + 1;
        foreach (var day in dailySummaries)
        {
            foreach (var entry in day.Entries)
            {
                ws.Cell(row, 1).Value = day.Date.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = entry.ChargeCode;
                ws.Cell(row, 3).Value = entry.Project;
                ws.Cell(row, 4).Value = entry.Task;
                ws.Cell(row, 5).Value = entry.Subtask;
                ws.Cell(row, 6).Value = entry.Hours;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 7).Value = entry.Notes;
                row++;
            }

            // day subtotal
            ws.Cell(row, 1).Value = day.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 5).Value = "Day Total:";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = day.TotalHours;
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            row++;
        }

        // date range selected total
        row++;
        ws.Cell(row, 5).Value = "Date Range Total:";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(row, 6).Value = dailySummaries.Sum(d => d.TotalHours);
        ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 6).Style.Font.FontSize = 12;

        // hrs by charge code
        row += 3;
        ws.Cell(row, 1).Value = "Hours by Charge Code";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 13;
        ws.Range(row, 1, row, 3).Merge();
        row++;

        var ccHeaders = new[] { "Charge Code", "Project", "Hours" };
        for (int i = 0; i < ccHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = ccHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            cell.Style.Font.FontColor = XLColor.White;
        }
        row++;

        foreach (var cc in chargeCodeSummaries)
        {
            ws.Cell(row, 1).Value = cc.Name;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = cc.TotalHours;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            row++;

            foreach (var proj in cc.Projects)
            {
                ws.Cell(row, 2).Value = proj.Name;
                ws.Cell(row, 3).Value = proj.TotalHours;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
                row++;
            }
        }

        // hrs by proj
        row += 2;
        ws.Cell(row, 1).Value = "Hours by Project";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 13;
        ws.Range(row, 1, row, 2).Merge();
        row++;

        var projHeaders = new[] { "Project", "Hours" };
        for (int i = 0; i < projHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = projHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            cell.Style.Font.FontColor = XLColor.White;
        }
        row++;

        foreach (var proj in projectSummaries)
        {
            ws.Cell(row, 1).Value = proj.Name;
            ws.Cell(row, 2).Value = proj.TotalHours;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Column(7).Width = 30;
    }

    private static void BuildOverviewSheet(
        XLWorkbook workbook,
        string userName,
        List<ChargeCodeSummary> chargeCodeSummaries,
        List<ProjectSummary> projectSummaries,
        decimal totalHours,
        DateOnly? oldestDate)
    {
        var ws = workbook.Worksheets.Add("Overview");

        // title
        ws.Cell(1, 1).Value = "All-Time Hours Overview";
        ws.Range(1, 1, 1, 3).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 2;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            ws.Cell(row, 1).Value = $"Name: {userName}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row++;
        }

        if (oldestDate.HasValue)
        {
            ws.Cell(row, 1).Value = $"Since (oldest entry): {oldestDate.Value:MMM d, yyyy}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row++;
        }

        ws.Cell(row, 1).Value = $"Total Hours: {totalHours:F2}";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;

        // by charge code
        row += 2;
        ws.Cell(row, 1).Value = "Hours by Charge Code";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 13;
        ws.Range(row, 1, row, 3).Merge();
        row++;

        var ccHeaders = new[] { "Charge Code", "Project", "Hours" };
        for (int i = 0; i < ccHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = ccHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            cell.Style.Font.FontColor = XLColor.White;
        }
        row++;

        foreach (var cc in chargeCodeSummaries)
        {
            ws.Cell(row, 1).Value = cc.Name;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = cc.TotalHours;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            row++;

            foreach (var proj in cc.Projects)
            {
                ws.Cell(row, 2).Value = proj.Name;
                ws.Cell(row, 3).Value = proj.TotalHours;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
                row++;
            }
        }

        // by project
        row += 2;
        ws.Cell(row, 1).Value = "Hours by Project";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 13;
        ws.Range(row, 1, row, 2).Merge();
        row++;

        var projHeaders = new[] { "Project", "Hours" };
        for (int i = 0; i < projHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = projHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            cell.Style.Font.FontColor = XLColor.White;
        }
        row++;

        foreach (var proj in projectSummaries)
        {
            ws.Cell(row, 1).Value = proj.Name;
            ws.Cell(row, 2).Value = proj.TotalHours;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            row++;
        }

        ws.Columns().AdjustToContents();
    }
}
