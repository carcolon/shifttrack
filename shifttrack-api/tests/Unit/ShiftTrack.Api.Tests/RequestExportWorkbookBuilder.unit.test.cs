using ClosedXML.Excel;
using ShiftTrack.Domain.Entities;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class RequestExportWorkbookBuilderTests
{
    [Fact]
    public void Build_CreatesWorkbookWithPtoSwapsAndDaysOffSheets()
    {
        var pto = new PtoRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserDisplayName = "Jane Doe",
            UserEmail = "jane.doe@example.com",
            RequestType = "vacations",
            NumberOfDays = 2,
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 2),
            Status = "approved",
            RequestedByEmail = "manager@example.com",
            RequestedByName = "Manager User",
            RequestedByRole = 1,
            ReviewedByEmail = "admin@example.com",
            ReviewedByName = "Admin User",
            ReviewedAtUtc = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc)
        };
        var dayOff = new PtoRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserDisplayName = "John Dayoff",
            UserEmail = "john.dayoff@example.com",
            RequestType = "day_off",
            NumberOfDays = 1,
            StartDate = new DateTime(2026, 9, 4),
            EndDate = new DateTime(2026, 9, 4),
            Status = "pending",
            RequestedByEmail = "john.dayoff@example.com",
            RequestedByName = "John Dayoff",
            RequestedByRole = 0,
            CreatedAtUtc = new DateTime(2026, 8, 31, 13, 0, 0, DateTimeKind.Utc)
        };
        var swap = new SwapRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestedByEmail = "requester@example.com",
            RequestedByDisplayName = "Requester User",
            RequestedByRole = 0,
            TargetUserId = Guid.NewGuid(),
            TargetUserEmail = "target@example.com",
            TargetUserDisplayName = "Target User",
            TargetUserRole = 0,
            SwapDate = new DateTime(2026, 9, 5),
            RequestedDatesJson = """["2026-09-05"]""",
            TargetDatesJson = """["2026-09-06"]""",
            RequestType = "swap_shift",
            Status = "denied",
            CreatedAtUtc = new DateTime(2026, 8, 31, 14, 0, 0, DateTimeKind.Utc)
        };

        var bytes = RequestExportWorkbookBuilder.Build([pto], [swap], [dayOff]);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(["PTO", "Swaps", "Days Off"], workbook.Worksheets.Select(sheet => sheet.Name).ToArray());
        Assert.Equal("ID Solicitud", workbook.Worksheet("PTO").Cell(1, 1).GetString());
        Assert.Equal("Jane Doe", workbook.Worksheet("PTO").Cell(2, 3).GetString());
        Assert.Equal("Requester User", workbook.Worksheet("Swaps").Cell(2, 3).GetString());
        Assert.Equal("John Dayoff", workbook.Worksheet("Days Off").Cell(2, 3).GetString());
        Assert.Equal(24, workbook.Worksheet("PTO").Row(1).Height);
        Assert.False(workbook.Worksheet("PTO").Cell(1, 1).Style.Alignment.WrapText);
        Assert.False(workbook.Worksheet("PTO").Cell(2, 4).Style.Alignment.WrapText);
        Assert.True(workbook.Worksheet("PTO").Cell(2, 16).Style.Alignment.WrapText);
    }
}
