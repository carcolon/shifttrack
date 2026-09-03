using ClosedXML.Excel;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class RequestExportWorkbookBuilder
{
    internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "ID Solicitud",
        "Fecha de requerimiento",
        "Nombre completo",
        "Correo",
        "Tipo Requerimiento",
        "Tipo de solicitud",
        "Dias de solicitud",
        "Fecha de inicio",
        "Fecha fin",
        "Correo solicitante",
        "Nombre solicitante",
        "Estado",
        "Fecha de aprobacion",
        "Correo aprobador",
        "Nombre completo aprobador",
        "Comentarios"
    ];

    internal static byte[] Build(
        IEnumerable<PtoRequest> ptoRequests,
        IEnumerable<SwapRequest> swapRequests,
        IEnumerable<PtoRequest> dayOffRequests)
    {
        using var workbook = new XLWorkbook();
        AddPtoSheet(workbook, "PTO", ptoRequests, "PTO");
        AddSwapSheet(workbook, "Swaps", swapRequests);
        AddPtoSheet(workbook, "Days Off", dayOffRequests, "Days Off");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddPtoSheet(XLWorkbook workbook, string sheetName, IEnumerable<PtoRequest> requests, string family)
    {
        var worksheet = CreateSheet(workbook, sheetName);
        var row = 2;

        foreach (var request in requests.OrderByDescending(item => item.CreatedAtUtc))
        {
            WriteRow(worksheet, row++,
                request.Id.ToString("D"),
                FormatDateTime(request.CreatedAtUtc),
                request.UserDisplayName,
                request.UserEmail,
                family,
                PtoHelpers.FormatPtoRequestTypeLabel(request.RequestType, family),
                request.NumberOfDays.ToString(),
                FormatDate(request.StartDate),
                FormatDate(request.EndDate),
                request.RequestedByEmail,
                request.RequestedByName,
                FormatStatus(request.Status),
                FormatDateTime(request.ReviewedAtUtc),
                request.ReviewedByEmail ?? string.Empty,
                request.ReviewedByName ?? string.Empty,
                BuildComments(request.Comments, request.ReviewComments));
        }

        FinishSheet(worksheet);
    }

    private static void AddSwapSheet(XLWorkbook workbook, string sheetName, IEnumerable<SwapRequest> requests)
    {
        var worksheet = CreateSheet(workbook, sheetName);
        var row = 2;

        foreach (var request in requests.OrderByDescending(item => item.CreatedAtUtc))
        {
            var requestedDates = SwapHelpers.DeserializeDateList(request.RequestedDatesJson);
            var targetDates = SwapHelpers.DeserializeDateList(request.TargetDatesJson);
            var allDates = requestedDates.Concat(targetDates)
                .Select(ParseDateOrNull)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .OrderBy(item => item)
                .ToArray();
            var days = Math.Max(requestedDates.Length, targetDates.Length);

            WriteRow(worksheet, row++,
                request.Id.ToString("D"),
                FormatDateTime(request.CreatedAtUtc),
                request.RequestedByDisplayName,
                request.RequestedByEmail,
                "Swaps",
                FormatSwapRequestType(request.RequestType),
                days.ToString(),
                allDates.Length > 0 ? FormatDate(allDates.First()) : FormatDate(request.SwapDate),
                allDates.Length > 0 ? FormatDate(allDates.Last()) : FormatDate(request.SwapDate),
                request.RequestedByEmail,
                request.RequestedByDisplayName,
                FormatStatus(request.Status),
                FormatDateTime(request.ReviewedAtUtc),
                request.ReviewedByEmail ?? string.Empty,
                request.ReviewedByName ?? string.Empty,
                BuildComments(request.Comments, request.ReviewComments));
        }

        FinishSheet(worksheet);
    }

    private static IXLWorksheet CreateSheet(XLWorkbook workbook, string sheetName)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);
        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = Headers[index];
        }

        var header = worksheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1f5f99");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        return worksheet;
    }

    private static void WriteRow(IXLWorksheet worksheet, int row, params string[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            worksheet.Cell(row, index + 1).Value = values[index];
        }
    }

    private static void FinishSheet(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null) return;

        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        usedRange.Style.Alignment.WrapText = false;

        var headerRow = worksheet.Row(1);
        headerRow.Height = 24;
        headerRow.Style.Alignment.WrapText = false;
        headerRow.Style.Alignment.ShrinkToFit = false;

        for (var column = 1; column <= Headers.Length; column++)
        {
            var worksheetColumn = worksheet.Column(column);
            worksheetColumn.AdjustToContents();
            worksheetColumn.Width = Math.Min(Math.Max(worksheetColumn.Width + 2, 10), column == Headers.Length ? 72 : 80);
            worksheetColumn.Style.Alignment.WrapText = false;
        }

        var commentsColumn = worksheet.Column(Headers.Length);
        commentsColumn.Width = Math.Min(Math.Max(commentsColumn.Width, 44), 72);
        commentsColumn.Style.Alignment.WrapText = true;

        if (usedRange.RowCount() > 1)
        {
            worksheet.Range(2, Headers.Length, usedRange.RowCount(), Headers.Length).Style.Alignment.WrapText = true;
            worksheet.Rows(2, usedRange.RowCount()).AdjustToContents();
        }
    }

    private static string FormatStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "approved" => "Approved",
            "denied" => "Denied",
            "canceled" => "Cancelled",
            "cancelled" => "Cancelled",
            _ => status
        };

    private static string FormatSwapRequestType(string requestType) =>
        requestType.Trim().ToLowerInvariant() switch
        {
            "swap_shift" => "Swap Shift",
            _ => requestType
        };

    private static string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd");

    private static string FormatDateTime(DateTime? date) => date?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

    private static DateTime? ParseDateOrNull(string value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.Date : null;

    private static string BuildComments(string? comments, string? reviewComments)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(comments)) values.Add(comments.Trim());
        if (!string.IsNullOrWhiteSpace(reviewComments)) values.Add($"Review: {reviewComments.Trim()}");
        return string.Join(Environment.NewLine, values);
    }
}
