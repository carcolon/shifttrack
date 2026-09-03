using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using ShiftTrack.Api;
using ShiftTrack.Application;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class BulkUserUploadHelpersTests
{
    [Fact]
    public async Task ReadRowsAsync_ParsesCsvCompaniesWithQuotedComma_AndCommaSeparatedDays()
    {
        await using var stream = BuildWorkbook("""
            <row r="10">
              <c r="A10" t="inlineStr"><is><t>First Name*</t></is></c>
              <c r="B10" t="inlineStr"><is><t>Last Name*</t></is></c>
              <c r="C10" t="inlineStr"><is><t>Email*</t></is></c>
              <c r="D10" t="inlineStr"><is><t>Role*</t></is></c>
              <c r="E10" t="inlineStr"><is><t>Location*</t></is></c>
              <c r="F10" t="inlineStr"><is><t>Companies*</t></is></c>
              <c r="G10" t="inlineStr"><is><t>Primary Company*</t></is></c>
              <c r="H10" t="inlineStr"><is><t>Operation*</t></is></c>
              <c r="I10" t="inlineStr"><is><t>Period Number*</t></is></c>
              <c r="J10" t="inlineStr"><is><t>Effective From*</t></is></c>
              <c r="K10" t="inlineStr"><is><t>Effective To</t></is></c>
              <c r="L10" t="inlineStr"><is><t>Shift Time*</t></is></c>
              <c r="M10" t="inlineStr"><is><t>Is Repeating?</t></is></c>
              <c r="N10" t="inlineStr"><is><t>Block Number*</t></is></c>
              <c r="O10" t="inlineStr"><is><t>Start*</t></is></c>
              <c r="P10" t="inlineStr"><is><t>End*</t></is></c>
              <c r="Q10" t="inlineStr"><is><t>Days*</t></is></c>
              <c r="R10" t="inlineStr"><is><t>Notes</t></is></c>
            </row>
            <row r="11">
              <c r="A11" t="inlineStr"><is><t>Jane</t></is></c>
              <c r="B11" t="inlineStr"><is><t>Doe</t></is></c>
              <c r="C11" t="inlineStr"><is><t>jane.doe@example.com</t></is></c>
              <c r="D11" t="inlineStr"><is><t>Team Leader</t></is></c>
              <c r="E11" t="inlineStr"><is><t>Colombia</t></is></c>
              <c r="F11" t="inlineStr"><is><t>Peter's pan,&quot;Esquire Law, LLC&quot;</t></is></c>
              <c r="G11" t="inlineStr"><is><t>Esquire Law, LLC</t></is></c>
              <c r="H11" t="inlineStr"><is><t>Leaders</t></is></c>
              <c r="I11" t="inlineStr"><is><t>1</t></is></c>
              <c r="J11" t="inlineStr"><is><t>2026-09-01</t></is></c>
              <c r="L11" t="inlineStr"><is><t>Morning</t></is></c>
              <c r="M11" t="inlineStr"><is><t>No</t></is></c>
              <c r="N11" t="inlineStr"><is><t>1</t></is></c>
              <c r="O11" t="inlineStr"><is><t>07:00</t></is></c>
              <c r="P11" t="inlineStr"><is><t>16:00</t></is></c>
              <c r="Q11" t="inlineStr"><is><t>Mon,Tue,Wed,Thu</t></is></c>
            </row>
            """);
        var file = new FormFile(stream, 0, stream.Length, "file", "bulk.xlsx");

        var (rows, errors) = await BulkUserUploadHelpers.ReadRowsAsync(file);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal(RoleHelpers.TeamLeader, row.Role);
        Assert.Equal(["Peter's pan", "Esquire Law, LLC"], row.Companies);
        Assert.Equal(["Mon", "Tue", "Wed", "Thu"], row.Days);
    }

    [Fact]
    public async Task ReadRowsAsync_ReturnsExactHeaderError_WhenHeaderWasRenamed()
    {
        await using var stream = BuildWorkbook("""
            <row r="10">
              <c r="A10" t="inlineStr"><is><t>First</t></is></c>
            </row>
            """);
        var file = new FormFile(stream, 0, stream.Length, "file", "bulk.xlsx");

        var (_, errors) = await BulkUserUploadHelpers.ReadRowsAsync(file);

        Assert.Contains(errors, error =>
            error.Row == 10 &&
            error.Column == "A" &&
            error.Message.Contains("Expected header 'First Name*'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadRowsAsync_ConvertsExcelDateAndTimeSerials()
    {
        await using var stream = BuildWorkbook("""
            <row r="10">
              <c r="A10" t="inlineStr"><is><t>First Name*</t></is></c>
              <c r="B10" t="inlineStr"><is><t>Last Name*</t></is></c>
              <c r="C10" t="inlineStr"><is><t>Email*</t></is></c>
              <c r="D10" t="inlineStr"><is><t>Role*</t></is></c>
              <c r="E10" t="inlineStr"><is><t>Location*</t></is></c>
              <c r="F10" t="inlineStr"><is><t>Companies*</t></is></c>
              <c r="G10" t="inlineStr"><is><t>Primary Company*</t></is></c>
              <c r="H10" t="inlineStr"><is><t>Operation*</t></is></c>
              <c r="I10" t="inlineStr"><is><t>Period Number*</t></is></c>
              <c r="J10" t="inlineStr"><is><t>Effective From*</t></is></c>
              <c r="K10" t="inlineStr"><is><t>Effective To</t></is></c>
              <c r="L10" t="inlineStr"><is><t>Shift Time*</t></is></c>
              <c r="M10" t="inlineStr"><is><t>Is Repeating?</t></is></c>
              <c r="N10" t="inlineStr"><is><t>Block Number*</t></is></c>
              <c r="O10" t="inlineStr"><is><t>Start*</t></is></c>
              <c r="P10" t="inlineStr"><is><t>End*</t></is></c>
              <c r="Q10" t="inlineStr"><is><t>Days*</t></is></c>
              <c r="R10" t="inlineStr"><is><t>Notes</t></is></c>
            </row>
            <row r="11">
              <c r="A11" t="inlineStr"><is><t>Jane</t></is></c>
              <c r="B11" t="inlineStr"><is><t>Doe</t></is></c>
              <c r="C11" t="inlineStr"><is><t>jane.doe@example.com</t></is></c>
              <c r="D11" t="inlineStr"><is><t>Employee</t></is></c>
              <c r="E11" t="inlineStr"><is><t>Colombia</t></is></c>
              <c r="F11" t="inlineStr"><is><t>Peter's pan</t></is></c>
              <c r="G11" t="inlineStr"><is><t>Peter's pan</t></is></c>
              <c r="H11" t="inlineStr"><is><t>Leaders</t></is></c>
              <c r="I11" t="inlineStr"><is><t>1</t></is></c>
              <c r="J11"><v>46266</v></c>
              <c r="L11" t="inlineStr"><is><t>Morning</t></is></c>
              <c r="M11" t="inlineStr"><is><t>No</t></is></c>
              <c r="N11" t="inlineStr"><is><t>1</t></is></c>
              <c r="O11"><v>0.2916666667</v></c>
              <c r="P11"><v>0.6666666667</v></c>
              <c r="Q11" t="inlineStr"><is><t>Mon,Tue</t></is></c>
            </row>
            """);
        var file = new FormFile(stream, 0, stream.Length, "file", "bulk.xlsx");

        var (rows, errors) = await BulkUserUploadHelpers.ReadRowsAsync(file);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal("2026-09-01", row.EffectiveFrom);
        Assert.Equal("07:00", row.Start);
        Assert.Equal("16:00", row.End);
    }

    [Fact]
    public async Task ReadRowsAsync_ResolvesAbsoluteWorksheetRelationshipTarget()
    {
        await using var stream = BuildWorkbook("""
            <row r="10">
              <c r="A10" t="inlineStr"><is><t>First Name*</t></is></c>
              <c r="B10" t="inlineStr"><is><t>Last Name*</t></is></c>
              <c r="C10" t="inlineStr"><is><t>Email*</t></is></c>
              <c r="D10" t="inlineStr"><is><t>Role*</t></is></c>
              <c r="E10" t="inlineStr"><is><t>Location*</t></is></c>
              <c r="F10" t="inlineStr"><is><t>Companies*</t></is></c>
              <c r="G10" t="inlineStr"><is><t>Primary Company*</t></is></c>
              <c r="H10" t="inlineStr"><is><t>Operation*</t></is></c>
              <c r="I10" t="inlineStr"><is><t>Period Number*</t></is></c>
              <c r="J10" t="inlineStr"><is><t>Effective From*</t></is></c>
              <c r="K10" t="inlineStr"><is><t>Effective To</t></is></c>
              <c r="L10" t="inlineStr"><is><t>Shift Time*</t></is></c>
              <c r="M10" t="inlineStr"><is><t>Is Repeating?</t></is></c>
              <c r="N10" t="inlineStr"><is><t>Block Number*</t></is></c>
              <c r="O10" t="inlineStr"><is><t>Start*</t></is></c>
              <c r="P10" t="inlineStr"><is><t>End*</t></is></c>
              <c r="Q10" t="inlineStr"><is><t>Days*</t></is></c>
              <c r="R10" t="inlineStr"><is><t>Notes</t></is></c>
            </row>
            <row r="11">
              <c r="A11" t="inlineStr"><is><t>Jane</t></is></c>
              <c r="B11" t="inlineStr"><is><t>Doe</t></is></c>
              <c r="C11" t="inlineStr"><is><t>jane.doe@example.com</t></is></c>
              <c r="D11" t="inlineStr"><is><t>Employee</t></is></c>
              <c r="E11" t="inlineStr"><is><t>Colombia</t></is></c>
              <c r="F11" t="inlineStr"><is><t>Peter's pan</t></is></c>
              <c r="G11" t="inlineStr"><is><t>Peter's pan</t></is></c>
              <c r="H11" t="inlineStr"><is><t>Leaders</t></is></c>
              <c r="I11" t="inlineStr"><is><t>1</t></is></c>
              <c r="J11" t="inlineStr"><is><t>2026-09-01</t></is></c>
              <c r="L11" t="inlineStr"><is><t>Morning</t></is></c>
              <c r="M11" t="inlineStr"><is><t>No</t></is></c>
              <c r="N11" t="inlineStr"><is><t>1</t></is></c>
              <c r="O11" t="inlineStr"><is><t>07:00</t></is></c>
              <c r="P11" t="inlineStr"><is><t>16:00</t></is></c>
              <c r="Q11" t="inlineStr"><is><t>Mon,Tue</t></is></c>
            </row>
            """, worksheetTarget: "/xl/worksheets/sheet1.xml");
        var file = new FormFile(stream, 0, stream.Length, "file", "bulk.xlsx");

        var (rows, errors) = await BulkUserUploadHelpers.ReadRowsAsync(file);

        Assert.Empty(errors);
        Assert.Single(rows);
    }

    private static MemoryStream BuildWorkbook(string rowsXml, string worksheetTarget = "worksheets/sheet1.xml")
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            AddEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Bulk Upload" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="{{worksheetTarget}}"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/worksheets/sheet1.xml", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    {{rowsXml}}
                  </sheetData>
                </worksheet>
                """);
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }
}
