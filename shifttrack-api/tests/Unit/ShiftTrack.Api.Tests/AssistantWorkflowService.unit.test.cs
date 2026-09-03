using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using ShiftTrack.Api;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Tests.Shared.Builders;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class AssistantWorkflowServiceTests
{
    private static readonly Guid JhonDoeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JhonSmithId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid JaneInactiveId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void ParseIntent_ReturnsDaysOffIntent_ForSingleNameInSpanish()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Dias off de Jhon");

        Assert.Equal(AssistantIntent.DaysOff, parsed.Intent);
        Assert.Equal(new[] { "jhon" }, parsed.NameTerms);
    }

    [Fact]
    public void ParseIntent_ReturnsWorkingDaysIntent_ForScheduleQuestion()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Schedule of Jhon Doe");

        Assert.Equal(AssistantIntent.WorkingDays, parsed.Intent);
        Assert.Equal(new[] { "jhon", "doe" }, parsed.NameTerms);
    }

    [Fact]
    public void ParseIntent_IgnoresCaseAndAccents_ForSpanishNameQueries()
    {
        var parsed = AssistantWorkflowService.ParseIntent("DIAS LIBRES DE JHÓN DÓE");

        Assert.Equal(AssistantIntent.DaysOff, parsed.Intent);
        Assert.Equal(new[] { "jhon", "doe" }, parsed.NameTerms);
    }

    [Fact]
    public void ParseIntent_ReturnsDaysOffIntent_ForTimeOffVariant()
    {
        var parsed = AssistantWorkflowService.ParseIntent("time off of Jhon Doe");

        Assert.Equal(AssistantIntent.DaysOff, parsed.Intent);
        Assert.Equal(new[] { "jhon", "doe" }, parsed.NameTerms);
    }

    [Fact]
    public void ParseIntent_StripsTrailingPunctuation_FromEmployeeName()
    {
        var parsed = AssistantWorkflowService.ParseIntent("days off of Sara Puerta?");

        Assert.Equal(AssistantIntent.DaysOff, parsed.Intent);
        Assert.Equal(new[] { "sara", "puerta" }, parsed.NameTerms);
        Assert.Equal("sara puerta", parsed.RawName);
    }

    [Fact]
    public void ParseIntent_UnderstandsMananaWithEnye_ForShiftQueries()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Quien trabaja en el turno de la mañana?");

        Assert.Equal(AssistantIntent.WhoWorksDay, parsed.Intent);
        Assert.Equal("Morning", parsed.Filters.ShiftTime);
    }

    [Fact]
    public void ParseIntent_ReturnsWhoHasDaysOff_ForDescansoVariant()
    {
        var parsed = AssistantWorkflowService.ParseIntent("quien descansa la siguiente semana?");

        Assert.Equal(AssistantIntent.WhoHasDaysOff, parsed.Intent);
        Assert.NotNull(parsed.WeekReference);
        Assert.Equal(1, parsed.WeekReference!.RelativeWeekOffset);
    }

    [Fact]
    public void ParseIntent_ReturnsDaysOffIntent_ForNotWorkingVariant()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Is Jhon Doe not working next monday?");

        Assert.Equal(AssistantIntent.DaysOff, parsed.Intent);
        Assert.Equal("Mon", parsed.DayOrStatus);
        Assert.Equal(new[] { "jhon", "doe" }, parsed.NameTerms);
    }

    [Fact]
    public void ParseIntent_ReturnsWhoHasDaysOff_ForNoTrabajaVariant()
    {
        var parsed = AssistantWorkflowService.ParseIntent("quien no trabaja el viernes?");

        Assert.Equal(AssistantIntent.WhoHasDaysOff, parsed.Intent);
        Assert.Equal("Fri", parsed.DayOrStatus);
    }

    [Fact]
    public void ParseIntent_ReturnsWhoWorksDayIntent_InSpanish()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Quien trabaja el lunes?");

        Assert.Equal(AssistantIntent.WhoWorksDay, parsed.Intent);
        Assert.Equal("Mon", parsed.DayOrStatus);
    }

    [Fact]
    public void ParseIntent_ReturnsWhoWorksDayIntent_ForSpecificEnglishDate()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Who works on March 17 of 2026?");

        Assert.Equal(AssistantIntent.WhoWorksDay, parsed.Intent);
        Assert.NotNull(parsed.DateReference);
        Assert.Equal(3, parsed.DateReference!.Month);
        Assert.Equal(17, parsed.DateReference.Day);
        Assert.Equal(2026, parsed.DateReference.Year);
    }

    [Fact]
    public void ParseIntent_ReturnsWhoWorksDayIntent_ForWorkedMessageWithRelativeWeek()
    {
        var parsed = AssistantWorkflowService.ParseIntent("who worked on Monday from last week?");

        Assert.Equal(AssistantIntent.WhoWorksDay, parsed.Intent);
        Assert.Equal("Mon", parsed.DayOrStatus);
        Assert.NotNull(parsed.WeekReference);
        Assert.Equal(-1, parsed.WeekReference!.RelativeWeekOffset);
    }

    [Fact]
    public void ParseIntent_ReturnsWorkingDaysIntent_ForRelativeWeekdayEmployeeQuestion()
    {
        var parsed = AssistantWorkflowService.ParseIntent("Does Jhon Doe work next monday?");

        Assert.Equal(AssistantIntent.WorkingDays, parsed.Intent);
        Assert.Equal("Mon", parsed.DayOrStatus);
        Assert.NotNull(parsed.WeekReference);
        Assert.Equal(1, parsed.WeekReference!.RelativeWeekOffset);
    }

    [Fact]
    public void ResolveRequestedWeekStart_UsesSpecificDateWeek_WhenAskingForDateWithoutYear()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var parsed = AssistantWorkflowService.ParseIntent("Who works on April 15?");

        var resolved = AssistantWorkflowService.ResolveRequestedWeekStart(selectedWeek, parsed);

        Assert.Equal(new DateTime(2026, 04, 13), resolved);
    }

    [Fact]
    public void ResolveRequestedWeekStart_UsesNaturalWeekReference()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var parsed = AssistantWorkflowService.ParseIntent("Who has PTO week of March 16 2026?");

        var resolved = AssistantWorkflowService.ResolveRequestedWeekStart(selectedWeek, parsed);

        Assert.Equal(new DateTime(2026, 03, 16), resolved);
    }

    [Fact]
    public void ResolveRequestedWeekStart_UsesPreviousWeek_WhenMessageAsksForLastWeek()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var parsed = AssistantWorkflowService.ParseIntent("Quien estuvo en PTO la semana pasada?");

        var resolved = AssistantWorkflowService.ResolveRequestedWeekStart(selectedWeek, parsed);

        Assert.Equal(new DateTime(2026, 03, 02), resolved);
    }

    [Fact]
    public void ResolveRequestedWeekStart_UsesNextWeek_WhenMessageAsksForNextWeek()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var parsed = AssistantWorkflowService.ParseIntent("Who has days off next week?");

        var resolved = AssistantWorkflowService.ResolveRequestedWeekStart(selectedWeek, parsed);

        Assert.Equal(new DateTime(2026, 03, 16), resolved);
    }

    [Fact]
    public async Task QueryAsync_ReturnsMultipleMatches_WhenSingleNameMatchesManyEmployees()
    {
        var monday = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Days off de Jhon", monday.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("days_off", body!.Intent);
        Assert.Equal(2, body.Matches.Length);
    }

    [Fact]
    public async Task QueryAsync_ReturnsWorkingDays_ForEmployeeSchedule()
    {
        var monday = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Horario de Jhon Doe", monday.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("working_days", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Contains(body.Matches[0].Facts, fact => fact.Type == "working_day" && fact.Label.Contains("Mon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmployeeWorkingStatus_ForSpecificRelativeWeekday()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Does Jhon Doe work next monday?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("working_days", body!.Intent);
        Assert.Equal("2026-03-16", body.WeekStart);
        Assert.Single(body.Matches);
        Assert.Single(body.Matches[0].Facts);
        Assert.Contains("Mar 16", body.Matches[0].Facts[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_AllowsQueriesByEmail()
    {
        var monday = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Days off of jhon.doe@company.com", monday.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("days_off", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("jhon.doe@company.com", body.Matches[0].Email);
    }

    [Fact]
    public async Task QueryAsync_ReturnsPtoEmployees_ForWhoIsOnPtoLastWeek()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var users = CreateUsers();
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((DateTime from, DateTime _) =>
            {
                if (from == new DateTime(2026, 03, 02))
                {
                    return new[]
                    {
                        new UserScheduleOverride
                        {
                            UserId = JhonDoeId,
                            OverrideDate = new DateTime(2026, 03, 04),
                            EntryType = "pto",
                            RequestType = "vacations",
                            Label = "PTO"
                        }
                    };
                }

                return Array.Empty<UserScheduleOverride>();
            });

        var service = new AssistantWorkflowService(users.Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who was on PTO last week?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_is_on_pto", body!.Intent);
        Assert.Equal("2026-03-02", body.WeekStart);
        Assert.Single(body.Matches);
    }

    [Fact]
    public async Task QueryAsync_ReturnsPtoEmployees_ForSpecificDate()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var users = CreateUsers();
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[]
            {
                new UserScheduleOverride
                {
                    UserId = JhonDoeId,
                    OverrideDate = new DateTime(2026, 03, 11),
                    EntryType = "pto",
                    RequestType = "vacations",
                    Label = "PTO"
                }
            });

        var service = new AssistantWorkflowService(users.Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who has PTO on March 11 2026?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_is_on_pto", body!.Intent);
        Assert.Equal("2026-03-09", body.WeekStart);
        Assert.Single(body.Matches);
        Assert.Contains("Mar 11", body.Matches[0].Facts[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_ReturnsWhoHasDaysOffNextWeek()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var users = CreateUsers();
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var service = new AssistantWorkflowService(users.Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Quien tiene dias libres la siguiente semana?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_has_days_off", body!.Intent);
        Assert.Equal("2026-03-16", body.WeekStart);
    }

    [Fact]
    public async Task QueryAsync_ReturnsWhoHasDaysOff_ForSpecificWeekday()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var users = CreateUsers();
        var service = new AssistantWorkflowService(users.Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who has days off on Friday?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_has_days_off", body!.Intent);
        Assert.Contains(body.Matches, match => match.DisplayName == "Jhon Smith");
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmployeeDayOff_ForSpecificDayQuestion()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Is Jhon Smith off on Friday?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("days_off", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Single(body.Matches[0].Facts);
        Assert.Contains("Mar 13", body.Matches[0].Facts[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseIntent_ExtractsOperationFilter_InEnglishAndSpanish()
    {
        var english = AssistantWorkflowService.ParseIntent("who works in leaders next week?");
        var spanish = AssistantWorkflowService.ParseIntent("quien trabaja en leaders la siguiente semana?");

        Assert.Equal("Leaders", english.Filters.Operation);
        Assert.Equal("Leaders", spanish.Filters.Operation);
    }

    [Fact]
    public void ParseIntent_ExtractsRoleCompanyAndLocationFilters()
    {
        var parsed = AssistantWorkflowService.ParseIntent("who has days off for managers in col company esquire law?");

        Assert.Equal(1, parsed.Filters.Role);
        Assert.Equal("COL", parsed.Filters.Location);
        Assert.Equal("Esquire Law, LLC", parsed.Filters.Company);
    }

    [Fact]
    public void ParseIntent_ExtractsShiftFilter_InEnglishAndSpanish()
    {
        var english = AssistantWorkflowService.ParseIntent("who are in the morning shift?");
        var spanish = AssistantWorkflowService.ParseIntent("quien trabaja en el turno de la tarde el lunes?");

        Assert.Equal("Morning", english.Filters.ShiftTime);
        Assert.Equal("Late", spanish.Filters.ShiftTime);
    }

    [Fact]
    public void ParseIntent_TreatsDirectOperationQuery_AsFilterMembership()
    {
        var parsed = AssistantWorkflowService.ParseIntent("leaders");

        Assert.Equal(AssistantIntent.WhoMatchesFilter, parsed.Intent);
        Assert.Equal("Leaders", parsed.Filters.Operation);
    }

    [Fact]
    public void ParseIntent_TreatsWhoAreLeaders_AsFilterMembership()
    {
        var parsed = AssistantWorkflowService.ParseIntent("who are leaders?");

        Assert.Equal(AssistantIntent.WhoMatchesFilter, parsed.Intent);
        Assert.Equal("Leaders", parsed.Filters.Operation);
    }

    [Fact]
    public void ParseIntent_TreatsCompanyMembershipQuestion_AsFilterMembership()
    {
        var parsed = AssistantWorkflowService.ParseIntent("quien pertenece a esquire law?");

        Assert.Equal(AssistantIntent.WhoMatchesFilter, parsed.Intent);
        Assert.Equal("Esquire Law, LLC", parsed.Filters.Company);
    }

    [Fact]
    public async Task QueryAsync_AppliesOperationFilter_CaseInsensitive()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("who works in leaders monday?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.All(body!.Matches, match => Assert.Equal("Jhon Doe", match.DisplayName));
    }

    [Fact]
    public async Task QueryAsync_AppliesSpanishOperationFilter()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("quien trabaja en outbound el lunes?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body!.Matches);
        Assert.Equal("Jhon Smith", body.Matches[0].DisplayName);
    }

    [Fact]
    public async Task QueryAsync_ReturnsMorningShiftEmployees_ForShiftOnlyQuestion()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("who are in the morning shift?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("Jhon Doe", body.Matches[0].DisplayName);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmployees_ForDirectOperationMembershipQuery()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("leaders", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_matches_filter", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("Jhon Doe", body.Matches[0].DisplayName);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmployees_ForCompanyMembershipQuery()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("quien pertenece a esquire law?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_matches_filter", body!.Intent);
        Assert.Equal(2, body.Matches.Length);
    }

    [Fact]
    public async Task QueryAsync_ReturnsLateShiftEmployees_ForSpanishDateQuestion()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("quien trabaja en el turno de la tarde el 17 de marzo de 2026?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("Jhon Smith", body.Matches[0].DisplayName);
    }

    [Fact]
    public async Task QueryAsync_ReturnsWorkers_ForSpecificDay()
    {
        var monday = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who works monday?", monday.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.NotEmpty(body.Matches);
        Assert.All(body.Matches.SelectMany(item => item.Facts), fact => Assert.Equal("working_day", fact.Type));
    }

    [Fact]
    public async Task QueryAsync_ReturnsWorkers_ForSpecificDate()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who works on March 17 of 2026?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.Equal("2026-03-16", body.WeekStart);
        Assert.NotEmpty(body.Matches);
        Assert.All(body.Matches.SelectMany(item => item.Facts), fact => Assert.Contains("Mar 17, 2026", fact.Label, StringComparison.Ordinal));
    }

    [Fact]
    public async Task QueryAsync_ReturnsWorkers_ForPreviousWeekDayPhrase()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("who worked on Monday from last week?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.Equal("2026-03-02", body.WeekStart);
        Assert.NotEmpty(body.Matches);
        Assert.All(body.Matches.SelectMany(item => item.Facts), fact => Assert.Contains("Mar 2, 2026", fact.Label, StringComparison.Ordinal));
    }

    [Fact]
    public async Task QueryAsync_ReturnsWorkers_ForSlashDate()
    {
        var selectedWeek = new DateTime(2026, 03, 09);
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who works on 17/03/2026?", selectedWeek.ToString("yyyy-MM-dd")));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_works_day", body!.Intent);
        Assert.Equal("2026-03-16", body.WeekStart);
        Assert.NotEmpty(body.Matches);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmployeeStatus()
    {
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Is Jhon Doe active?", "2026-03-09"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("employee_status", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("active", body.Matches[0].Facts[0].Type);
    }

    [Fact]
    public async Task QueryAsync_ReturnsInactiveEmployees()
    {
        var service = new AssistantWorkflowService(CreateUsers().Object);
        var context = BuildHttpContext();

        var result = await service.QueryAsync(
            context,
            new AssistantQueryRequest("Who is inactive?", "2026-03-09"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        var body = ResultTestHelpers.ReadJson<AssistantQueryResponse>(response.Body);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("who_is_inactive", body!.Intent);
        Assert.Single(body.Matches);
        Assert.Equal("Jane Inactive", body.Matches[0].DisplayName);
    }

    private static Mock<IUserRepository> CreateUsers()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .SystemHidden()
            .WithCompany("Esquire Law, LLC")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithId(JhonDoeId)
                .WithEmail("jhon.doe@company.com")
                .WithDisplayName("Jhon Doe")
                .AsEmployee()
                .WithLocation("COL")
                .WithCompany("Esquire Law, LLC")
                .WithOperation("Leaders")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(JhonSmithId)
                .WithEmail("jhon.smith@company.com")
                .WithDisplayName("Jhon Smith")
                .AsEmployee()
                .WithLocation("COL")
                .WithCompany("Esquire Law, LLC")
                .WithOperation("Outbound")
                .WithShiftTime("Late")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(JaneInactiveId)
                .WithEmail("jane.inactive@company.com")
                .WithDisplayName("Jane Inactive")
                .AsEmployee()
                .Inactive()
                .WithLocation("COL")
                .WithCompany("Esquire Law, LLC")
                .WithOperation("Support")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());
        return users;
    }

    private static HttpContext BuildHttpContext()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "1"),
            new Claim("role", "1"),
            new Claim(ClaimTypes.Email, "manager@company.com"),
            new Claim(ClaimTypes.Name, "Manager"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
