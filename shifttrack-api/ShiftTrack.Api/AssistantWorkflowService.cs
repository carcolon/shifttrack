using System.Globalization;
using System.Text.RegularExpressions;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal interface IAssistantWorkflowService
{
    Task<IResult> QueryAsync(HttpContext httpContext, AssistantQueryRequest request);
}

internal sealed class AssistantWorkflowService : IAssistantWorkflowService
{
    private static readonly Regex EmailRegex = new(
        @"[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DaysOffRegex = new(
        @"(?:\b(?:cuales?\s+son\s+los?\s+)?(?:dias?\s+off|days?\s+off|off\s+days?|day\s+off|free\s+days?|time\s+off|dias?\s+libres?|dias?\s+de\s+descanso|descanso)\b(?:\s+(?:de|of|for))?\s+(?<name>.+)$)|(?:\b(?:is|was|does|did|esta|estaba|tiene|tuvo)\s+(?<name2>.+?)\s+(?:off|free|libre|dia\s+off|dias?\s+off|free\s+days?|time\s+off|dias?\s+libres?|dias?\s+de\s+descanso|descanso)\b)|(?:\b(?:when\s+is|when\s+was|cuando\s+esta|cuando\s+estaba|cuando\s+tiene|cuando\s+tuvo)\s+(?<name3>.+?)\s+(?:off|free|libre|dia\s+off|dias?\s+off|free\s+days?|time\s+off|dias?\s+libres?|dias?\s+de\s+descanso|descanso)\b)|(?:\b(?:is|was|does|did|esta|estaba)\s+(?<name4>.+?)\s+(?:not\s+working|not\s+scheduled|not\s+programmed|no\s+trabaja|no\s+esta\s+programad[oa])\b)|(?:\b(?:when\s+is|when\s+was|cuando)\s+(?<name5>.+?)\s+(?:not\s+working|not\s+scheduled|no\s+trabaja|no\s+esta\s+programad[oa])\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WorkingDaysRegex = new(
        @"(?:\b(?:horario|schedule|working\s+days|dias?\s+laborales?|turno|shift)\b(?:\s+(?:de|of|for))?\s+(?<name>.+)$)|(?:\b(?:does|did|is|was|esta|estaba)\s+(?<name2>.+?)\s+(?:work|working|scheduled|trabaja(?:r)?|programad[oa])\b)|(?:\b(?:when\s+does|when\s+did|when\s+is|when\s+was|what\s+is|what'?s|cuando\s+trabaja|que\s+horario\s+tiene|cual\s+es\s+el\s+horario\s+de)\s+(?<name3>.+?)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NegativeWorkingRegex = new(
        @"\b(?:is|was|does|did|esta|estaba)\s+(?<name>.+?)\s+(?:not\s+working|not\s+scheduled|not\s+programmed|no\s+trabaja|no\s+esta\s+programad[oa])\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmployeePtoRegex = new(
        @"(?:\b(?:pto|vacations|vacaciones|permisos?)\b(?:\s+(?:de|of|for))?\s+(?<name>.+)$)|(?:\b(?:is|was|does|did|esta|estaba|tiene|tuvo)\s+(?<name2>.+?)\s+(?:on\s+)?(?:pto|vacations|vacaciones|permisos?)\b)|(?:\b(?:when\s+is|when\s+was|cuando\s+esta|cuando\s+estaba|cuando\s+tiene|cuando\s+tuvo)\s+(?<name3>.+?)\s+(?:on\s+)?(?:pto|vacations|vacaciones|permisos?)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhoIsOnPtoRegex = new(
        @"\b(?:who\s+(?:is|was|has|had)\s+(?:on\s+)?pto|quien(?:es)?\s+(?:esta|estan|estuvo|estuvieron|tiene|tienen|tuvo|tuvieron)\s+en\s+pto|quien(?:es)?\s+tiene(?:n)?\s+pto)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhoHasDaysOffRegex = new(
        @"\b(?:who\s+has\s+days?\s+off|who\s+has\s+free\s+days?|who\s+has\s+time\s+off|who\s+is\s+off|who\s+is\s+not\s+working|who\s+is\s+not\s+scheduled|quien(?:es)?\s+tiene(?:n)?\s+dias?\s+libres?|quien(?:es)?\s+tiene(?:n)?\s+dias?\s+de\s+descanso|quien(?:es)?\s+esta(?:n)?\s+libre(?:s)?|quien(?:es)?\s+descansa(?:n)?|quien(?:es)?\s+no\s+trabaja(?:n)?|quien(?:es)?\s+no\s+esta(?:n)?\s+programad[oa]s?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhoWorksIntentRegex = new(
        @"\b(?:who\b.*\b(?:work|works|worked|working|scheduled)\b|who\s+(?:is|are)\b.*\b(?:in\s+the\s+)?(?:morning|late)\s+shift\b|quien(?:es)?\b.*\b(?:trabaja(?:n)?|trabajo|trabajo|trabajaron|trabajando|programad[oa]s?)\b|quien(?:es)?\s+(?:esta(?:n)?\s+)?(?:en\s+)?(?:el\s+)?turno\s+de\s+la\s+(?:manana|tarde)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmployeeStatusRegex = new(
        @"\b(?:is|esta)\s+(?<name>[a-z0-9@.\s]+?)\s+(?<status>active|inactive|activo|inactivo)\b|\b(?<status2>active|inactive|activo|inactivo)\s+(?:employee\s+)?(?<name2>[a-z0-9@.\s]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhoIsStatusRegex = new(
        @"\b(?:who\s+is|who\s+are|quien(?:es)?\s+esta(?:n)?|quien(?:es)?\s+son)\s+(?<status>active|inactive|activo|inactivo)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WhoBelongsRegex = new(
        @"\b(?:who\s+(?:is|are|belongs?\s+to)|employees?\s+(?:in|from|for)|members?\s+(?:in|from|for)|quien(?:es)?\s+(?:son|pertenece(?:n)?\s+a)|empleados?\s+(?:en|de|para)|miembros?\s+(?:en|de|para))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WeekOfDateRegex = new(
        @"\b(?:week\s+of|semana\s+del?|semana\s+de)\s+(?<date>\d{4}-\d{2}-\d{2}|[a-z]+\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|\d{1,2}\s+de\s+[a-z]+(?:\s+de\s+\d{4})?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IsoDateRegex = new(
        @"^(?<date>\d{4}-\d{2}-\d{2})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IsoDateInTextRegex = new(
        @"(?<date>\d{4}-\d{2}-\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DayMonthYearDateRegex = new(
        @"^(?<day>\d{1,2})[/-](?<month>\d{1,2})[/-](?<year>\d{4})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DayMonthYearDateInTextRegex = new(
        @"(?<date>\d{1,2}[/-]\d{1,2}[/-]\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EnglishMonthDateRegex = new(
        @"^(?<month>january|february|march|april|may|june|july|august|september|october|november|december)\s+(?<day>\d{1,2})(?:,?\s+(?:of\s+)?)?(?<year>\d{4})?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EnglishMonthDateInTextRegex = new(
        @"(?<date>january\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|february\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|march\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|april\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|may\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|june\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|july\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|august\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|september\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|october\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|november\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?|december\s+\d{1,2}(?:,?\s+(?:of\s+)??\d{4})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpanishMonthDateRegex = new(
        @"^(?<day>\d{1,2})(?:\s+de)?\s+(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?:\s+de\s+(?<year>\d{4}))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpanishMonthDateInTextRegex = new(
        @"(?<date>\d{1,2}(?:\s+de)?\s+(?:enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?:\s+de\s+\d{4})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DayOfWeekInTextRegex = new(
        @"\b(?<day>monday|lunes|tuesday|martes|wednesday|miercoles|thursday|jueves|friday|viernes|saturday|sabado|sunday|domingo)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RelativeWeekdayLeadingRegex = new(
        @"\b(?<rel>this|next|last|este|esta|proximo|proxima|pasado|pasada)\s+(?<day>monday|lunes|tuesday|martes|wednesday|miercoles|thursday|jueves|friday|viernes|saturday|sabado|sunday|domingo)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RelativeWeekdayTrailingRegex = new(
        @"\b(?<day>monday|lunes|tuesday|martes|wednesday|miercoles|thursday|jueves|friday|viernes|saturday|sabado|sunday|domingo)\s+(?<rel>this|next|last|actual|actuales|proximo|proxima|pasado|pasada)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex OperationFilterRegex = new(
        @"\b(?:operation|operacion|op|in|en)\s+(?<value>leaders|outbound|referral|esq|sgf)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompanyFilterRegex = new(
        @"\b(?:company|empresa|compania)\s+(?<value>solvo\s+global|sgf)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocationFilterRegex = new(
        @"\b(?:location|ubicacion|sede|in|en)\s+(?<value>col|arg|wpb)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RoleFilterRegex = new(
        @"\b(?:role|rol|for|para)\s+(?<value>admin(?:s|istrador(?:es)?)?|manager(?:s)?|gerente(?:s)?|employee(?:s)?|empleado(?:s)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ShiftFilterRegex = new(
        @"\b(?:(?<value>morning|late)\s+shift|shift\s+(?:of\s+)?(?<value2>morning|late)|turno\s+(?:de\s+la\s+|de\s+)?(?<value3>manana|tarde)|(?<value4>manana|tarde)\s+shift)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] ThisWeekPhrases = ["this week", "esta semana", "current week", "semana actual"];
    private static readonly string[] NextWeekPhrases = ["next week", "la siguiente semana", "siguiente semana", "proxima semana", "semana siguiente"];
    private static readonly string[] LastWeekPhrases = ["last week", "past week", "la semana pasada", "semana pasada"];
    private static readonly string[] TrailingWeekPhrases =
    [
        " esta semana",
        " this week",
        " current week",
        " la siguiente semana",
        " siguiente semana",
        " next week",
        " la semana pasada",
        " semana pasada",
        " last week",
        " past week",
        " from this week",
        " for this week",
        " from next week",
        " for next week",
        " from last week",
        " for last week",
        " de esta semana",
        " para esta semana",
        " de la siguiente semana",
        " para la siguiente semana",
        " de la semana pasada",
        " para la semana pasada"
    ];
    private static readonly string[] TodayPhrases = ["today", "hoy"];
    private static readonly string[] TomorrowPhrases = ["tomorrow", "manana"];
    private static readonly string[] YesterdayPhrases = ["yesterday", "ayer"];

    private static readonly IReadOnlyDictionary<string, int> MonthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["january"] = 1,
        ["february"] = 2,
        ["march"] = 3,
        ["april"] = 4,
        ["may"] = 5,
        ["june"] = 6,
        ["july"] = 7,
        ["august"] = 8,
        ["september"] = 9,
        ["october"] = 10,
        ["november"] = 11,
        ["december"] = 12,
        ["enero"] = 1,
        ["febrero"] = 2,
        ["marzo"] = 3,
        ["abril"] = 4,
        ["mayo"] = 5,
        ["junio"] = 6,
        ["julio"] = 7,
        ["agosto"] = 8,
        ["septiembre"] = 9,
        ["setiembre"] = 9,
        ["octubre"] = 10,
        ["noviembre"] = 11,
        ["diciembre"] = 12
    };
    private static readonly IReadOnlyDictionary<string, string> OperationMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["leaders"] = "Leaders",
        ["outbound"] = "Outbound",
        ["referral"] = "Referral",
        ["esq"] = "ESQ",
        ["sgf"] = "SGF"
    };
    private static readonly IReadOnlyDictionary<string, string> CompanyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["esquire law"] = "Esquire Law, LLC",
        ["esquire law llc"] = "Esquire Law, LLC",
        ["esquire law, llc"] = "Esquire Law, LLC"
    };
    private static readonly IReadOnlyDictionary<string, string> LocationMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["col"] = "COL",
        ["arg"] = "ARG",
        ["wpb"] = "WPB"
    };
    private static readonly IReadOnlyDictionary<string, int> RoleMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = 2,
        ["admins"] = 2,
        ["administrador"] = 2,
        ["administradores"] = 2,
        ["manager"] = 1,
        ["managers"] = 1,
        ["gerente"] = 1,
        ["gerentes"] = 1,
        ["employee"] = 0,
        ["employees"] = 0,
        ["empleado"] = 0,
        ["empleados"] = 0
    };
    private static readonly IReadOnlyDictionary<string, string> ShiftMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["morning"] = "Morning",
        ["manana"] = "Morning",
        ["late"] = "Late",
        ["tarde"] = "Late"
    };

    private readonly IUserRepository _users;

    public AssistantWorkflowService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IResult> QueryAsync(HttpContext httpContext, AssistantQueryRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || callerContext.Role < 0)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return Results.BadRequest(new ErrorResponse("Message is required."));
        }

        var parsed = ParseIntent(message);
        var selectedWeekStart = ResolveWeekStartFromQuery(request.WeekStart) ?? ResolveWeekStart(DateTime.UtcNow.Date);
        var weekStart = ResolveRequestedWeekStart(selectedWeekStart, parsed);

        if (parsed.Intent == AssistantIntent.Unknown)
        {
            return Results.Ok(new AssistantQueryResponse
            {
                Intent = "unknown",
                Status = "unsupported",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = "I can help with days off, work schedule, PTO, who is on PTO, who has days off, who works on a weekday or exact date, specific weeks, and active or inactive employees. You can ask in English or Spanish."
            });
        }

        if (parsed.Intent is AssistantIntent.EmployeeStatus or AssistantIntent.WhoIsActive or AssistantIntent.WhoIsInactive)
        {
            var activeUsersForStatus = ScopeUsersForCaller(callerContext, callerUser, await _users.GetAllAsync());
            var inactiveUsersForStatus = ScopeUsersForCaller(callerContext, callerUser, await _users.GetInactiveAsync());
            var allUsersForStatus = activeUsersForStatus
                .Concat(inactiveUsersForStatus)
                .GroupBy(user => user.Id)
                .Select(group => group.First())
                .ToArray();
            var filteredUsersForStatus = ApplyUserFilters(allUsersForStatus, parsed.Filters);

            return Results.Ok(HandleStatusIntent(filteredUsersForStatus, parsed, weekStart));
        }

        if (parsed.Intent == AssistantIntent.WhoMatchesFilter)
        {
            var scopedUsers = ScopeUsersForCaller(callerContext, callerUser, await _users.GetAllAsync());
            var filteredUsers = ApplyUserFilters(scopedUsers, parsed.Filters);
            return Results.Ok(HandleFilterMembershipIntent(filteredUsers, parsed, weekStart));
        }

        var allUsers = ApplyUserFilters(ScopeUsersForCaller(callerContext, callerUser, await _users.GetAllAsync()), parsed.Filters);
        var activeUsers = allUsers.Where(user => user.IsActive).ToArray();

        var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToArray();
        var overrides = (await _users.GetScheduleOverridesAsync(weekStart, weekStart.AddDays(6))).ToArray();
        var overrideMap = overrides.ToDictionary(
            item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}",
            item => item,
            StringComparer.OrdinalIgnoreCase);
        var rows = activeUsers
            .Where(user => HasCalendarPresence(user, days, overrideMap))
            .Select(user => BuildCalendarRow(user, days, overrideMap))
            .ToArray();

        return parsed.Intent switch
        {
            AssistantIntent.DaysOff => Results.Ok(HandleEmployeeCalendarIntent(rows, days, parsed, "days_off", "day_off", IsDayOffCell, BuildDaysOffMessage, weekStart)),
            AssistantIntent.WorkingDays => Results.Ok(HandleEmployeeCalendarIntent(rows, days, parsed, "working_days", "working_day", IsWorkingCell, BuildWorkingDaysMessage, weekStart)),
            AssistantIntent.EmployeePto => Results.Ok(HandleEmployeeCalendarIntent(rows, days, parsed, "employee_pto", "pto", IsPtoCell, BuildEmployeePtoMessage, weekStart)),
            AssistantIntent.WhoIsOnPto => Results.Ok(HandleGlobalIntent(rows, days, parsed, "who_is_on_pto", "pto", IsPtoCell, BuildWhoIsOnPtoMessage, weekStart)),
            AssistantIntent.WhoHasDaysOff => Results.Ok(HandleGlobalIntent(rows, days, parsed, "who_has_days_off", "day_off", IsDayOffCell, BuildWhoHasDaysOffMessage, weekStart)),
            AssistantIntent.WhoWorksDay => Results.Ok(HandleWhoWorksDayIntent(rows, days, parsed, selectedWeekStart, weekStart)),
            _ => Results.Ok(new AssistantQueryResponse
            {
                Intent = "unknown",
                Status = "unsupported",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = "Unsupported request."
            })
        };
    }

    internal static AssistantIntentMatch ParseIntent(string message)
    {
        var normalized = NormalizeSearch(message);
        var weekReference = ParseWeekReference(normalized);
        var filters = ParseFilters(normalized);

        var whoIsStatusMatch = WhoIsStatusRegex.Match(normalized);
        if (whoIsStatusMatch.Success)
        {
            var status = NormalizeStatus(whoIsStatusMatch.Groups["status"].Value);
            return new AssistantIntentMatch(
                status == "active" ? AssistantIntent.WhoIsActive : AssistantIntent.WhoIsInactive,
                string.Empty,
                Array.Empty<string>(),
                null,
                null,
                weekReference,
                normalized,
                filters);
        }

        var employeeStatusMatch = EmployeeStatusRegex.Match(normalized);
        if (employeeStatusMatch.Success)
        {
            var status = NormalizeStatus(employeeStatusMatch.Groups["status"].Success
                ? employeeStatusMatch.Groups["status"].Value
                : employeeStatusMatch.Groups["status2"].Value);
            var rawName = CleanNameCandidate(employeeStatusMatch.Groups["name"].Success
                ? employeeStatusMatch.Groups["name"].Value
                : employeeStatusMatch.Groups["name2"].Value);
            return new AssistantIntentMatch(
                AssistantIntent.EmployeeStatus,
                rawName,
                TokenizeName(rawName),
                status,
                null,
                weekReference,
                normalized,
                filters);
        }

        if (ShouldTreatAsFilterMembership(normalized, filters))
        {
            return new AssistantIntentMatch(
                AssistantIntent.WhoMatchesFilter,
                string.Empty,
                Array.Empty<string>(),
                null,
                null,
                weekReference,
                normalized,
                filters);
        }

        if (WhoHasDaysOffRegex.IsMatch(normalized))
        {
            return new AssistantIntentMatch(
                AssistantIntent.WhoHasDaysOff,
                string.Empty,
                Array.Empty<string>(),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var globalDaysOffDate) ? globalDaysOffDate : null,
                weekReference,
                normalized,
                filters);
        }

        if (WhoIsOnPtoRegex.IsMatch(normalized))
        {
            return new AssistantIntentMatch(
                AssistantIntent.WhoIsOnPto,
                string.Empty,
                Array.Empty<string>(),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var globalPtoDate) ? globalPtoDate : null,
                weekReference,
                normalized,
                filters);
        }

        var negativeWorkingMatch = NegativeWorkingRegex.Match(normalized);
        if (negativeWorkingMatch.Success)
        {
            var rawName = CleanNameCandidate(negativeWorkingMatch.Groups["name"].Value);
            return new AssistantIntentMatch(
                AssistantIntent.DaysOff,
                rawName,
                TokenizeName(rawName),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var negativeWorkingDate) ? negativeWorkingDate : null,
                weekReference,
                normalized,
                filters);
        }

        var employeePtoMatch = EmployeePtoRegex.Match(normalized);
        if (employeePtoMatch.Success)
        {
            var rawName = CleanNameCandidate(GetFirstGroupValue(employeePtoMatch, "name", "name2", "name3"));
            return new AssistantIntentMatch(
                AssistantIntent.EmployeePto,
                rawName,
                TokenizeName(rawName),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var employeePtoDate) ? employeePtoDate : null,
                weekReference,
                normalized,
                filters);
        }

        if (WhoWorksIntentRegex.IsMatch(normalized))
        {
            if (TryFindDateReferenceInText(normalized, out var dateReference))
            {
                return new AssistantIntentMatch(
                    AssistantIntent.WhoWorksDay,
                    string.Empty,
                    Array.Empty<string>(),
                    null,
                    dateReference,
                    weekReference,
                    normalized,
                    filters);
            }

            return new AssistantIntentMatch(
                AssistantIntent.WhoWorksDay,
                string.Empty,
                Array.Empty<string>(),
                TryExtractDayCode(normalized),
                null,
                weekReference,
                normalized,
                filters);
        }

        var workingDaysMatch = WorkingDaysRegex.Match(normalized);
        if (workingDaysMatch.Success)
        {
            var rawName = CleanNameCandidate(GetFirstGroupValue(workingDaysMatch, "name", "name2", "name3"));
            return new AssistantIntentMatch(
                AssistantIntent.WorkingDays,
                rawName,
                TokenizeName(rawName),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var workingDate) ? workingDate : null,
                weekReference,
                normalized,
                filters);
        }

        var daysOffMatch = DaysOffRegex.Match(normalized);
        if (daysOffMatch.Success)
        {
            var rawName = CleanNameCandidate(GetFirstGroupValue(daysOffMatch, "name", "name2", "name3", "name4", "name5"));
            return new AssistantIntentMatch(
                AssistantIntent.DaysOff,
                rawName,
                TokenizeName(rawName),
                TryExtractDayCode(normalized),
                TryFindDateReferenceInText(normalized, out var dayOffDate) ? dayOffDate : null,
                weekReference,
                normalized,
                filters);
        }

        return new AssistantIntentMatch(AssistantIntent.Unknown, string.Empty, Array.Empty<string>(), null, null, weekReference, normalized, filters);
    }

    internal static DateTime ResolveRequestedWeekStart(DateTime selectedWeekStart, AssistantIntentMatch parsed)
    {
        if (TryResolveSpecificDate(selectedWeekStart, parsed, out var specificDate))
        {
            return ResolveWeekStart(specificDate);
        }

        if (parsed.WeekReference?.ExplicitWeekStart is DateTime explicitWeekStart)
        {
            return explicitWeekStart;
        }

        if (parsed.WeekReference?.RelativeWeekOffset is int offset)
        {
            return selectedWeekStart.AddDays(offset * 7);
        }

        return selectedWeekStart;
    }

    private static AssistantQueryResponse HandleEmployeeCalendarIntent(
        CalendarRow[] rows,
        DateTime[] days,
        AssistantIntentMatch parsed,
        string intent,
        string factType,
        Func<CalendarCell, bool> predicate,
        Func<AssistantEmployeeResult[], string, DateTime, DateTime?, string> messageFactory,
        DateTime selectedWeekStart)
    {
        if (parsed.NameTerms.Length == 0)
        {
            return new AssistantQueryResponse
            {
                Intent = intent,
                Status = "unsupported",
                WeekStart = days[0].ToString("yyyy-MM-dd"),
                Message = "Please include an employee name."
            };
        }

        var matches = FindMatches(rows, parsed.NameTerms);
        if (matches.Length == 0)
        {
            return new AssistantQueryResponse
            {
                Intent = intent,
                Status = "not_found",
                WeekStart = days[0].ToString("yyyy-MM-dd"),
                Message = $"I couldn't find any active employee matching \"{parsed.RawName}\"."
            };
        }

        var targetDate = TryResolveTargetDate(selectedWeekStart, parsed, days, out var resolvedTargetDate)
            ? resolvedTargetDate
            : (DateTime?)null;
        if ((parsed.DateReference is not null || LooksLikeDayCode(parsed.DayOrStatus)) && targetDate is null)
        {
            return new AssistantQueryResponse
            {
                Intent = intent,
                Status = "unsupported",
                WeekStart = days[0].ToString("yyyy-MM-dd"),
                Message = "I couldn't resolve the requested date."
            };
        }

        var dayMap = BuildDayLabelMap(days);
        var results = matches
            .Select(row =>
            {
                var cells = row.Cells.Where(cell => predicate(cell));
                if (targetDate is not null)
                {
                    var targetKey = targetDate.Value.ToString("yyyy-MM-dd");
                    cells = cells.Where(cell => cell.Date == targetKey);
                }

                return BuildEmployeeResult(row, factType, cells, dayMap);
            })
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssistantQueryResponse
        {
            Intent = intent,
            Status = "ok",
            WeekStart = days[0].ToString("yyyy-MM-dd"),
            Message = messageFactory(results, parsed.RawName, days[0], targetDate),
            Matches = results
        };
    }

    private static AssistantQueryResponse HandleGlobalIntent(
        CalendarRow[] rows,
        DateTime[] days,
        AssistantIntentMatch parsed,
        string intent,
        string factType,
        Func<CalendarCell, bool> predicate,
        Func<AssistantEmployeeResult[], DateTime, DateTime?, string> messageFactory,
        DateTime weekStart)
    {
        var targetDate = TryResolveTargetDate(weekStart, parsed, days, out var resolvedTargetDate)
            ? resolvedTargetDate
            : (DateTime?)null;
        if ((parsed.DateReference is not null || LooksLikeDayCode(parsed.DayOrStatus)) && targetDate is null)
        {
            return new AssistantQueryResponse
            {
                Intent = intent,
                Status = "unsupported",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = "I couldn't resolve the requested date."
            };
        }

        var dayMap = BuildDayLabelMap(days);
        var results = rows
            .Select(row =>
            {
                var cells = row.Cells.Where(cell => predicate(cell));
                if (targetDate is not null)
                {
                    var targetKey = targetDate.Value.ToString("yyyy-MM-dd");
                    cells = cells.Where(cell => cell.Date == targetKey);
                }

                return BuildEmployeeResult(row, factType, cells, dayMap);
            })
            .Where(result => result.Facts.Length > 0)
            .OrderBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssistantQueryResponse
        {
            Intent = intent,
            Status = results.Length > 0 ? "ok" : "empty",
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            Message = messageFactory(results, weekStart, targetDate),
            Matches = results
        };
    }

    private static AssistantQueryResponse HandleWhoWorksDayIntent(
        CalendarRow[] rows,
        DateTime[] days,
        AssistantIntentMatch parsed,
        DateTime selectedWeekStart,
        DateTime weekStart)
    {
        if (parsed.Filters.ShiftTime is not null &&
            !LooksLikeDayCode(parsed.DayOrStatus) &&
            parsed.DateReference is null)
        {
            return HandleWhoWorksShiftWeekIntent(rows, days, parsed, weekStart);
        }

        if (!TryResolveTargetDate(selectedWeekStart, parsed, days, out var targetDate))
        {
            return new AssistantQueryResponse
            {
                Intent = "who_works_day",
                Status = "unsupported",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = "Please specify a weekday or an exact date."
            };
        }

        var targetKey = targetDate.ToString("yyyy-MM-dd");
        var label = targetDate.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        var results = rows
            .Select(row => new
            {
                Row = row,
                Cell = row.Cells.FirstOrDefault(cell => cell.Date == targetKey && IsWorkingCell(cell))
            })
            .Where(item => item.Cell is not null)
            .Select(item => new AssistantEmployeeResult
            {
                EmployeeId = item.Row.Id.ToString(),
                DisplayName = item.Row.DisplayName,
                Email = item.Row.Email,
                Facts = new[]
                {
                    new AssistantCalendarFact
                    {
                        Type = "working_day",
                        Date = targetKey,
                        Label = $"{label}: {item.Cell!.Label}"
                    }
                }
            })
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssistantQueryResponse
        {
            Intent = "who_works_day",
            Status = results.Length > 0 ? "ok" : "empty",
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            Message = results.Length > 0
                ? $"{results.Length} active employees work on {label}."
                : $"No active employees work on {label}.",
            Matches = results
        };
    }

    private static AssistantQueryResponse HandleWhoWorksShiftWeekIntent(
        CalendarRow[] rows,
        DateTime[] days,
        AssistantIntentMatch parsed,
        DateTime weekStart)
    {
        var dayMap = BuildDayLabelMap(days);
        var results = rows
            .Select(row => BuildEmployeeResult(row, "working_day", row.Cells.Where(IsWorkingCell), dayMap))
            .Where(result => result.Facts.Length > 0)
            .OrderBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var shiftLabel = parsed.Filters.ShiftTime ?? "selected";
        return new AssistantQueryResponse
        {
            Intent = "who_works_day",
            Status = results.Length > 0 ? "ok" : "empty",
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            Message = results.Length > 0
                ? $"{results.Length} active employees are in the {shiftLabel} shift for the week of {weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}."
                : $"No active employees are in the {shiftLabel} shift for the week of {weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}.",
            Matches = results
        };
    }

    private static AssistantQueryResponse HandleStatusIntent(User[] allUsers, AssistantIntentMatch parsed, DateTime weekStart)
    {
        if (parsed.Intent is AssistantIntent.WhoIsActive or AssistantIntent.WhoIsInactive)
        {
            var desiredActive = parsed.Intent == AssistantIntent.WhoIsActive;
            var statusMatches = allUsers
                .Where(user => user.IsActive == desiredActive)
                .OrderBy(user => user.DisplayName ?? user.Email, StringComparer.OrdinalIgnoreCase)
                .Select(user => new AssistantEmployeeResult
                {
                    EmployeeId = user.Id.ToString(),
                    DisplayName = user.DisplayName ?? user.Email,
                    Email = user.Email,
                    Facts = new[]
                    {
                        new AssistantCalendarFact
                        {
                            Type = desiredActive ? "active" : "inactive",
                            Date = string.Empty,
                            Label = desiredActive ? "Active employee" : "Inactive employee"
                        }
                    }
                })
                .ToArray();

            return new AssistantQueryResponse
            {
                Intent = desiredActive ? "who_is_active" : "who_is_inactive",
                Status = statusMatches.Length > 0 ? "ok" : "empty",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = statusMatches.Length > 0
                    ? $"{statusMatches.Length} employees are {(desiredActive ? "active" : "inactive")}."
                    : $"No employees are {(desiredActive ? "active" : "inactive")}.",
                Matches = statusMatches
            };
        }

        if (parsed.NameTerms.Length == 0)
        {
            return new AssistantQueryResponse
            {
                Intent = "employee_status",
                Status = "unsupported",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = "Please include an employee name."
            };
        }

        var matches = allUsers.Where(user =>
        {
            var haystack = NormalizeSearch($"{user.DisplayName} {user.Email}");
            return parsed.NameTerms.All(term => haystack.Contains(term, StringComparison.Ordinal));
        })
        .Select(user => new AssistantEmployeeResult
        {
            EmployeeId = user.Id.ToString(),
            DisplayName = user.DisplayName ?? user.Email,
            Email = user.Email,
            Facts = new[]
            {
                new AssistantCalendarFact
                {
                    Type = user.IsActive ? "active" : "inactive",
                    Date = string.Empty,
                    Label = user.IsActive ? "Active" : "Inactive"
                }
            }
        })
        .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

        if (matches.Length == 0)
        {
            return new AssistantQueryResponse
            {
                Intent = "employee_status",
                Status = "not_found",
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Message = $"I couldn't find any employee matching \"{parsed.RawName}\"."
            };
        }

        var desired = NormalizeStatus(parsed.DayOrStatus ?? string.Empty);
        var filtered = string.IsNullOrWhiteSpace(desired)
            ? matches
            : matches.Where(item => item.Facts[0].Type == desired).ToArray();

        var finalMatches = filtered.Length > 0 ? filtered : matches;
        return new AssistantQueryResponse
        {
            Intent = "employee_status",
            Status = "ok",
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            Message = finalMatches.Length == 1
                ? $"{finalMatches[0].DisplayName} is {finalMatches[0].Facts[0].Label.ToLowerInvariant()}."
                : $"I found {finalMatches.Length} employees matching \"{parsed.RawName}\".",
            Matches = finalMatches
        };
    }

    private static AssistantQueryResponse HandleFilterMembershipIntent(User[] users, AssistantIntentMatch parsed, DateTime weekStart)
    {
        var matches = users
            .Where(user => user.IsActive)
            .OrderBy(user => user.DisplayName ?? user.Email, StringComparer.OrdinalIgnoreCase)
            .Select(user => new AssistantEmployeeResult
            {
                EmployeeId = user.Id.ToString(),
                DisplayName = user.DisplayName ?? user.Email,
                Email = user.Email,
                Facts = new[]
                {
                    new AssistantCalendarFact
                    {
                        Type = "working_day",
                        Date = string.Empty,
                        Label = BuildMembershipFactLabel(user)
                    }
                }
            })
            .ToArray();

        var filterSummary = DescribeFilters(parsed.Filters);
        return new AssistantQueryResponse
        {
            Intent = "who_matches_filter",
            Status = matches.Length > 0 ? "ok" : "empty",
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            Message = matches.Length > 0
                ? $"{matches.Length} active employees match {filterSummary}."
                : $"No active employees match {filterSummary}.",
            Matches = matches
        };
    }

    private static AssistantEmployeeResult BuildEmployeeResult(
        CalendarRow row,
        string factType,
        IEnumerable<CalendarCell> cells,
        IReadOnlyDictionary<string, string> dayMap)
    {
        return new AssistantEmployeeResult
        {
            EmployeeId = row.Id.ToString(),
            DisplayName = row.DisplayName,
            Email = row.Email,
            Facts = cells
                .Select(cell => new AssistantCalendarFact
                {
                    Type = factType,
                    Date = cell.Date,
                    Label = BuildFactLabel(cell, dayMap)
                })
                .ToArray()
        };
    }

    private static string BuildFactLabel(CalendarCell cell, IReadOnlyDictionary<string, string> dayMap)
    {
        var prefix = dayMap.TryGetValue(cell.Date, out var label) ? label : cell.Date;
        return cell.Type switch
        {
            "dayOff" => prefix,
            "leave" => $"{prefix}: PTO",
            _ => $"{prefix}: {cell.Label}"
        };
    }

    private static IReadOnlyDictionary<string, string> BuildDayLabelMap(IEnumerable<DateTime> days) =>
        days.ToDictionary(day => day.ToString("yyyy-MM-dd"), day => day.ToString("ddd MMM d", CultureInfo.InvariantCulture));

    private static CalendarRow[] FindMatches(IEnumerable<CalendarRow> rows, IReadOnlyCollection<string> nameTerms)
    {
        if (nameTerms.Count == 0) return Array.Empty<CalendarRow>();

        var emailTerms = nameTerms.Where(term => EmailRegex.IsMatch(term)).ToArray();
        return rows.Where(row =>
        {
            var haystack = NormalizeSearch($"{row.DisplayName} {row.Email}");
            if (emailTerms.Length > 0)
            {
                return emailTerms.Any(email => string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase));
            }
            return nameTerms.All(term => haystack.Contains(term, StringComparison.Ordinal));
        }).ToArray();
    }

    private static bool IsDayOffCell(CalendarCell cell) =>
        string.Equals(cell.Type, "dayOff", StringComparison.OrdinalIgnoreCase);

    private static bool IsPtoCell(CalendarCell cell) =>
        string.Equals(cell.Type, "leave", StringComparison.OrdinalIgnoreCase);

    private static string BuildDaysOffMessage(AssistantEmployeeResult[] results, string rawName, DateTime weekStart, DateTime? targetDate)
    {
        var rangeLabel = $"{weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}";
        var targetLabel = targetDate?.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        if (results.Length == 1)
        {
            var employee = results[0];
            if (targetDate is not null)
            {
                return employee.Facts.Length == 0
                    ? $"{employee.DisplayName} does not have a day off on {targetLabel}."
                    : $"{employee.DisplayName} has a day off on {targetLabel}.";
            }

            return employee.Facts.Length == 0
                ? $"{employee.DisplayName} has no days off in the week of {rangeLabel}."
                : $"{employee.DisplayName} has day off on {string.Join(", ", employee.Facts.Select(fact => fact.Label))}.";
        }

        return targetDate is not null
            ? $"I found {results.Length} active employees matching \"{rawName}\" for {targetLabel}."
            : $"I found {results.Length} active employees matching \"{rawName}\" for the week of {rangeLabel}.";
    }

    private static string BuildWorkingDaysMessage(AssistantEmployeeResult[] results, string rawName, DateTime weekStart, DateTime? targetDate)
    {
        var rangeLabel = $"{weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}";
        var targetLabel = targetDate?.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        if (results.Length == 1)
        {
            var employee = results[0];
            if (targetDate is not null)
            {
                return employee.Facts.Length == 0
                    ? $"{employee.DisplayName} is not scheduled to work on {targetLabel}."
                    : $"{employee.DisplayName} works on {string.Join(", ", employee.Facts.Select(fact => fact.Label))}.";
            }

            return employee.Facts.Length == 0
                ? $"{employee.DisplayName} has no scheduled working days in the week of {rangeLabel}."
                : $"{employee.DisplayName} works on {string.Join(", ", employee.Facts.Select(fact => fact.Label))}.";
        }

        return targetDate is not null
            ? $"I found {results.Length} active employees matching \"{rawName}\" for {targetLabel}."
            : $"I found {results.Length} active employees matching \"{rawName}\" for the week of {rangeLabel}.";
    }

    private static string BuildEmployeePtoMessage(AssistantEmployeeResult[] results, string rawName, DateTime weekStart, DateTime? targetDate)
    {
        var rangeLabel = $"{weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}";
        var targetLabel = targetDate?.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        if (results.Length == 1)
        {
            var employee = results[0];
            if (targetDate is not null)
            {
                return employee.Facts.Length == 0
                    ? $"{employee.DisplayName} does not have PTO on {targetLabel}."
                    : $"{employee.DisplayName} has PTO on {targetLabel}.";
            }

            return employee.Facts.Length == 0
                ? $"{employee.DisplayName} has no PTO in the week of {rangeLabel}."
                : $"{employee.DisplayName} has PTO on {string.Join(", ", employee.Facts.Select(fact => fact.Label))}.";
        }

        return targetDate is not null
            ? $"I found {results.Length} active employees matching \"{rawName}\" for {targetLabel}."
            : $"I found {results.Length} active employees matching \"{rawName}\" for the week of {rangeLabel}.";
    }

    private static string BuildWhoIsOnPtoMessage(AssistantEmployeeResult[] results, DateTime weekStart, DateTime? targetDate)
    {
        var rangeLabel = $"{weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}";
        var targetLabel = targetDate?.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        if (targetDate is not null)
        {
            return results.Length > 0
                ? $"{results.Length} active employees have PTO on {targetLabel}."
                : $"No active employees have PTO on {targetLabel}.";
        }

        return results.Length > 0
            ? $"{results.Length} active employees have PTO in the week of {rangeLabel}."
            : $"No active employees have PTO in the week of {rangeLabel}.";
    }

    private static string BuildWhoHasDaysOffMessage(AssistantEmployeeResult[] results, DateTime weekStart, DateTime? targetDate)
    {
        var rangeLabel = $"{weekStart:MMM d} - {weekStart.AddDays(6):MMM d, yyyy}";
        var targetLabel = targetDate?.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);
        if (targetDate is not null)
        {
            return results.Length > 0
                ? $"{results.Length} active employees have days off on {targetLabel}."
                : $"No active employees have days off on {targetLabel}.";
        }

        return results.Length > 0
            ? $"{results.Length} active employees have days off in the week of {rangeLabel}."
            : $"No active employees have days off in the week of {rangeLabel}.";
    }

    private static AssistantWeekReference ParseWeekReference(string normalizedMessage)
    {
        var explicitMatch = WeekOfDateRegex.Match(normalizedMessage);
        if (explicitMatch.Success &&
            TryParseFlexibleDate(explicitMatch.Groups["date"].Value, DateTime.UtcNow.Year, out var parsed))
        {
            return new AssistantWeekReference(ResolveWeekStart(parsed.Date), null);
        }

        if (ContainsAny(normalizedMessage, NextWeekPhrases))
        {
            return new AssistantWeekReference(null, 1);
        }

        if (ContainsAny(normalizedMessage, LastWeekPhrases))
        {
            return new AssistantWeekReference(null, -1);
        }

        if (ContainsAny(normalizedMessage, ThisWeekPhrases))
        {
            return new AssistantWeekReference(null, 0);
        }

        var relativeWeekdayLeading = RelativeWeekdayLeadingRegex.Match(normalizedMessage);
        if (relativeWeekdayLeading.Success)
        {
            return new AssistantWeekReference(null, ResolveRelativeWeekOffset(relativeWeekdayLeading.Groups["rel"].Value));
        }

        var relativeWeekdayTrailing = RelativeWeekdayTrailingRegex.Match(normalizedMessage);
        if (relativeWeekdayTrailing.Success)
        {
            return new AssistantWeekReference(null, ResolveRelativeWeekOffset(relativeWeekdayTrailing.Groups["rel"].Value));
        }

        return new AssistantWeekReference(null, null);
    }

    private static bool ContainsAny(string value, IEnumerable<string> patterns) =>
        patterns.Any(pattern => value.Contains(pattern, StringComparison.Ordinal));

    private static string CleanNameCandidate(string value)
    {
        var cleaned = value.Trim();
        foreach (var suffix in TrailingWeekPhrases)
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length].Trim();
                break;
            }
        }

        var explicitWeekMatch = WeekOfDateRegex.Match(cleaned);
        if (explicitWeekMatch.Success)
        {
            cleaned = cleaned.Replace(explicitWeekMatch.Value, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        cleaned = DayMonthYearDateInTextRegex.Replace(cleaned, string.Empty).Trim();
        cleaned = EnglishMonthDateInTextRegex.Replace(cleaned, string.Empty).Trim();
        cleaned = SpanishMonthDateInTextRegex.Replace(cleaned, string.Empty).Trim();
        cleaned = RelativeWeekdayLeadingRegex.Replace(cleaned, string.Empty).Trim();
        cleaned = RelativeWeekdayTrailingRegex.Replace(cleaned, string.Empty).Trim();
        cleaned = Regex.Replace(cleaned, @"\b(?:today|tomorrow|yesterday|hoy|manana|ayer|on|el|para|for)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = cleaned.Trim('?', '!', '.', ',', ';', ':', '"', '\'').Trim();

        return cleaned;
    }

    private static bool TryResolveTargetDate(DateTime selectedWeekStart, AssistantIntentMatch parsed, DateTime[] days, out DateTime targetDate)
    {
        if (TryResolveSpecificDate(selectedWeekStart, parsed, out targetDate))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(parsed.DayOrStatus))
        {
            targetDate = days.FirstOrDefault(day => string.Equals(ApiHelpers.DayAbbrev(day.DayOfWeek), parsed.DayOrStatus, StringComparison.OrdinalIgnoreCase));
            return targetDate != default;
        }

        targetDate = default;
        return false;
    }

    private static bool TryResolveSpecificDate(DateTime selectedWeekStart, AssistantIntentMatch parsed, out DateTime targetDate)
    {
        if (parsed.DateReference is null)
        {
            targetDate = default;
            return false;
        }

        var year = parsed.DateReference.Year ?? selectedWeekStart.Year;
        if (!DateTime.TryParseExact(
                $"{year:D4}-{parsed.DateReference.Month:D2}-{parsed.DateReference.Day:D2}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out targetDate))
        {
            targetDate = default;
            return false;
        }

        return true;
    }

    private static bool TryParseDateReference(string value, out AssistantDateReference? dateReference)
    {
        var normalized = NormalizeSearch(value).Trim('?', '.', ',', ';', ':');

        if (ContainsAny(normalized, TodayPhrases))
        {
            var today = DateTime.UtcNow.Date;
            dateReference = new AssistantDateReference(today.Month, today.Day, today.Year);
            return true;
        }

        if (ContainsAny(normalized, TomorrowPhrases))
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            dateReference = new AssistantDateReference(tomorrow.Month, tomorrow.Day, tomorrow.Year);
            return true;
        }

        if (ContainsAny(normalized, YesterdayPhrases))
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            dateReference = new AssistantDateReference(yesterday.Month, yesterday.Day, yesterday.Year);
            return true;
        }

        var isoMatch = IsoDateRegex.Match(normalized);
        if (isoMatch.Success &&
            DateTime.TryParseExact(isoMatch.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
        {
            dateReference = new AssistantDateReference(isoDate.Month, isoDate.Day, isoDate.Year);
            return true;
        }

        var slashMatch = DayMonthYearDateRegex.Match(normalized);
        if (slashMatch.Success &&
            int.TryParse(slashMatch.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var slashDay) &&
            int.TryParse(slashMatch.Groups["month"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var slashMonth) &&
            int.TryParse(slashMatch.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var slashYear))
        {
            dateReference = new AssistantDateReference(slashMonth, slashDay, slashYear);
            return true;
        }

        var englishMatch = EnglishMonthDateRegex.Match(normalized);
        if (englishMatch.Success &&
            MonthMap.TryGetValue(englishMatch.Groups["month"].Value, out var englishMonth) &&
            int.TryParse(englishMatch.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var englishDay))
        {
            var hasYear = int.TryParse(englishMatch.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var englishYear);
            dateReference = new AssistantDateReference(englishMonth, englishDay, hasYear ? englishYear : null);
            return true;
        }

        var spanishMatch = SpanishMonthDateRegex.Match(normalized);
        if (spanishMatch.Success &&
            MonthMap.TryGetValue(spanishMatch.Groups["month"].Value, out var spanishMonth) &&
            int.TryParse(spanishMatch.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var spanishDay))
        {
            var hasYear = int.TryParse(spanishMatch.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var spanishYear);
            dateReference = new AssistantDateReference(spanishMonth, spanishDay, hasYear ? spanishYear : null);
            return true;
        }

        dateReference = null;
        return false;
    }

    private static bool TryFindDateReferenceInText(string normalized, out AssistantDateReference? dateReference)
    {
        foreach (var candidate in FindDateCandidates(normalized))
        {
            if (TryParseDateReference(candidate, out dateReference))
            {
                return true;
            }
        }

        dateReference = null;
        return false;
    }

    private static IEnumerable<string> FindDateCandidates(string normalized)
    {
        if (ContainsAny(normalized, TodayPhrases)) yield return "today";
        if (ContainsAny(normalized, TomorrowPhrases)) yield return "tomorrow";
        if (ContainsAny(normalized, YesterdayPhrases)) yield return "yesterday";

        var slashMatch = DayMonthYearDateInTextRegex.Match(normalized);
        if (slashMatch.Success) yield return slashMatch.Groups["date"].Value;

        var isoMatch = IsoDateInTextRegex.Match(normalized);
        if (isoMatch.Success) yield return isoMatch.Groups["date"].Value;

        var englishMatch = EnglishMonthDateInTextRegex.Match(normalized);
        if (englishMatch.Success) yield return englishMatch.Groups["date"].Value;

        var spanishMatch = SpanishMonthDateInTextRegex.Match(normalized);
        if (spanishMatch.Success) yield return spanishMatch.Groups["date"].Value;
    }

    private static bool TryParseFlexibleDate(string value, int fallbackYear, out DateTime date)
    {
        var normalized = NormalizeSearch(value).Trim('?', '.', ',', ';', ':');

        if (ContainsAny(normalized, TodayPhrases))
        {
            date = DateTime.UtcNow.Date;
            return true;
        }

        if (ContainsAny(normalized, TomorrowPhrases))
        {
            date = DateTime.UtcNow.Date.AddDays(1);
            return true;
        }

        if (ContainsAny(normalized, YesterdayPhrases))
        {
            date = DateTime.UtcNow.Date.AddDays(-1);
            return true;
        }

        var isoMatch = IsoDateRegex.Match(normalized);
        if (isoMatch.Success &&
            DateTime.TryParseExact(isoMatch.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        var englishMatch = EnglishMonthDateRegex.Match(normalized);
        if (englishMatch.Success &&
            TryBuildDate(
                englishMatch.Groups["month"].Value,
                englishMatch.Groups["day"].Value,
                englishMatch.Groups["year"].Value,
                fallbackYear,
                out date))
        {
            return true;
        }

        var spanishMatch = SpanishMonthDateRegex.Match(normalized);
        if (spanishMatch.Success &&
            TryBuildDate(
                spanishMatch.Groups["month"].Value,
                spanishMatch.Groups["day"].Value,
                spanishMatch.Groups["year"].Value,
                fallbackYear,
                out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool TryBuildDate(string monthToken, string dayToken, string yearToken, int fallbackYear, out DateTime date)
    {
        if (!MonthMap.TryGetValue(monthToken, out var month) ||
            !int.TryParse(dayToken, NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            date = default;
            return false;
        }

        var year = int.TryParse(yearToken, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
            ? parsedYear
            : fallbackYear;

        if (!DateTime.TryParseExact(
                $"{year:D4}-{month:D2}-{day:D2}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            date = default;
            return false;
        }

        return true;
    }

    private static string[] TokenizeName(string rawName)
    {
        var emails = EmailRegex.Matches(rawName)
            .Select(match => match.Value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (emails.Length > 0)
        {
            return emails;
        }

        return rawName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ResolveDayCode(string value)
    {
        return NormalizeSearch(value) switch
        {
            "monday" or "lunes" => "Mon",
            "tuesday" or "martes" => "Tue",
            "wednesday" or "miercoles" => "Wed",
            "thursday" or "jueves" => "Thu",
            "friday" or "viernes" => "Fri",
            "saturday" or "sabado" => "Sat",
            "sunday" or "domingo" => "Sun",
            _ => null
        };
    }

    private static string? TryExtractDayCode(string normalizedMessage)
    {
        var leadingRelativeMatch = RelativeWeekdayLeadingRegex.Match(normalizedMessage);
        if (leadingRelativeMatch.Success)
        {
            return ResolveDayCode(leadingRelativeMatch.Groups["day"].Value);
        }

        var trailingRelativeMatch = RelativeWeekdayTrailingRegex.Match(normalizedMessage);
        if (trailingRelativeMatch.Success)
        {
            return ResolveDayCode(trailingRelativeMatch.Groups["day"].Value);
        }

        var match = DayOfWeekInTextRegex.Match(normalizedMessage);
        return match.Success ? ResolveDayCode(match.Groups["day"].Value) : null;
    }

    private static string NormalizeStatus(string value)
    {
        return NormalizeSearch(value) switch
        {
            "activo" or "active" => "active",
            "inactivo" or "inactive" => "inactive",
            _ => string.Empty
        };
    }

    private static AssistantFilters ParseFilters(string normalized)
    {
        var operation = TryResolveMappedValue(OperationFilterRegex.Match(normalized), OperationMap)
            ?? TryResolveMappedValueFromMessage(normalized, OperationMap);
        var company = TryResolveMappedValue(CompanyFilterRegex.Match(normalized), CompanyMap)
            ?? TryResolveMappedValueFromMessage(normalized, CompanyMap);
        var location = TryResolveMappedValue(LocationFilterRegex.Match(normalized), LocationMap)
            ?? TryResolveMappedValueFromMessage(normalized, LocationMap);
        var role = TryResolveMappedRole(RoleFilterRegex.Match(normalized))
            ?? TryResolveMappedRoleFromMessage(normalized);
        var shiftTime = TryResolveMappedShift(ShiftFilterRegex.Match(normalized));
        return new AssistantFilters(operation, company, location, role, shiftTime);
    }

    private static User[] ApplyUserFilters(User[] users, AssistantFilters filters)
    {
        return users.Where(user =>
            (filters.Operation is null || string.Equals(user.Operation, filters.Operation, StringComparison.OrdinalIgnoreCase)) &&
            (filters.Company is null || string.Equals(user.Company, filters.Company, StringComparison.OrdinalIgnoreCase)) &&
            (filters.Location is null || string.Equals(user.Location, filters.Location, StringComparison.OrdinalIgnoreCase)) &&
            (!filters.Role.HasValue || user.Role == filters.Role.Value) &&
            (filters.ShiftTime is null || string.Equals(user.ShiftTime, filters.ShiftTime, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string? TryResolveMappedValue(Match match, IReadOnlyDictionary<string, string> map)
    {
        if (!match.Success) return null;
        var key = NormalizeSearch(match.Groups["value"].Value);
        return map.TryGetValue(key, out var value) ? value : null;
    }

    private static string? TryResolveMappedValueFromMessage(string normalized, IReadOnlyDictionary<string, string> map)
    {
        foreach (var key in map.Keys.OrderByDescending(value => value.Length))
        {
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return map[key];
            }
        }

        return null;
    }

    private static int? TryResolveMappedRole(Match match)
    {
        if (!match.Success) return null;
        var key = NormalizeSearch(match.Groups["value"].Value);
        return RoleMap.TryGetValue(key, out var value) ? value : null;
    }

    private static int? TryResolveMappedRoleFromMessage(string normalized)
    {
        foreach (var key in RoleMap.Keys.OrderByDescending(value => value.Length))
        {
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return RoleMap[key];
            }
        }

        return null;
    }

    private static string? TryResolveMappedShift(Match match)
    {
        if (!match.Success) return null;

        var key = GetFirstGroupValue(match, "value", "value2", "value3", "value4");
        if (string.IsNullOrWhiteSpace(key)) return null;

        key = NormalizeSearch(key);
        return ShiftMap.TryGetValue(key, out var value) ? value : null;
    }

    private static string GetFirstGroupValue(Match match, params string[] groupNames)
    {
        foreach (var groupName in groupNames)
        {
            if (match.Groups[groupName].Success)
            {
                var value = match.Groups[groupName].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static bool LooksLikeDayCode(string? value) =>
        value is "Mon" or "Tue" or "Wed" or "Thu" or "Fri" or "Sat" or "Sun";

    private static bool HasAnyFilter(AssistantFilters filters) =>
        filters.Operation is not null ||
        filters.Company is not null ||
        filters.Location is not null ||
        filters.Role.HasValue ||
        filters.ShiftTime is not null;

    private static bool ShouldTreatAsFilterMembership(string normalized, AssistantFilters filters)
    {
        if (!HasAnyFilter(filters))
        {
            return false;
        }

        if (filters.ShiftTime is not null &&
            filters.Operation is null &&
            filters.Company is null &&
            filters.Location is null &&
            !filters.Role.HasValue &&
            WhoWorksIntentRegex.IsMatch(normalized))
        {
            return false;
        }

        return WhoBelongsRegex.IsMatch(normalized) || IsFilterOnlyQuery(normalized, filters);
    }

    private static bool IsFilterOnlyQuery(string normalized, AssistantFilters filters)
    {
        var tokens = new List<string>();
        if (filters.Operation is not null) tokens.Add(NormalizeSearch(filters.Operation));
        if (filters.Company is not null) tokens.Add(NormalizeSearch(filters.Company));
        if (filters.Location is not null) tokens.Add(NormalizeSearch(filters.Location));
        if (filters.ShiftTime is not null) tokens.Add(NormalizeSearch(filters.ShiftTime));
        if (filters.Role.HasValue)
        {
            tokens.Add(filters.Role.Value switch
            {
                2 => "admin",
                1 => "manager",
                _ => "employee"
            });
        }

        var reduced = normalized;
        foreach (var token in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            reduced = Regex.Replace(reduced, $@"\b{Regex.Escape(token)}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        reduced = Regex.Replace(reduced, @"\b(?:who|who\s+are|who\s+is|who\s+belongs?\s+to|employees?|members?|quien(?:es)?|son|pertenece(?:n)?|empleados?|miembros?|in|from|for|en|de|para|the|el|la|los|las)\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        reduced = Regex.Replace(reduced, @"\s+", " ").Trim(' ', '?', '.', '!', ',');
        return string.IsNullOrWhiteSpace(reduced);
    }

    private static string DescribeFilters(AssistantFilters filters)
    {
        var parts = new List<string>();
        if (filters.Operation is not null) parts.Add($"operation {filters.Operation}");
        if (filters.Company is not null) parts.Add($"company {filters.Company}");
        if (filters.Location is not null) parts.Add($"location {filters.Location}");
        if (filters.Role.HasValue)
        {
            parts.Add(filters.Role.Value switch
            {
                2 => "role Admin",
                1 => "role Manager",
                _ => "role Employee"
            });
        }
        if (filters.ShiftTime is not null) parts.Add($"shift {filters.ShiftTime}");

        return parts.Count > 0 ? string.Join(", ", parts) : "the selected filters";
    }

    private static string BuildMembershipFactLabel(User user)
    {
        var roleLabel = user.Role switch
        {
            2 => "Admin",
            1 => "Manager",
            _ => "Employee"
        };

        return $"{roleLabel} | {user.Operation} | {user.Company} | {user.Location} | {user.ShiftTime}";
    }

    private static int ResolveRelativeWeekOffset(string value)
    {
        return NormalizeSearch(value) switch
        {
            "next" or "proximo" or "proxima" => 1,
            "last" or "pasado" or "pasada" => -1,
            "this" or "este" or "esta" or "actual" or "actuales" => 0,
            _ => 0
        };
    }

    private async Task<User?> ResolveCallerUserAsync(CallerContext callerContext)
    {
        if (callerContext.UserId.HasValue)
        {
            var byId = await _users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await _users.GetByEmailAsync(callerContext.Email);
    }

    private static User[] ScopeUsersForCaller(CallerContext callerContext, User callerUser, IEnumerable<User> users)
    {
        if (RoleHelpers.IsEmployeeLike(callerContext.Role))
        {
            return users
                .Where(user =>
                    (callerContext.UserId.HasValue && user.Id == callerContext.UserId.Value) ||
                    string.Equals(user.Email, callerContext.Email, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        return users
            .Where(user => IsInCallerCompanyScope(callerUser, user))
            .ToArray();
    }

    private static bool IsInCallerCompanyScope(User callerUser, User targetUser) =>
        CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, targetUser);
}

internal enum AssistantIntent
{
    Unknown,
    DaysOff,
    WorkingDays,
    EmployeePto,
    WhoIsOnPto,
    WhoHasDaysOff,
    WhoWorksDay,
    WhoMatchesFilter,
    EmployeeStatus,
    WhoIsActive,
    WhoIsInactive
}

internal sealed record AssistantWeekReference(DateTime? ExplicitWeekStart, int? RelativeWeekOffset);

internal sealed record AssistantDateReference(int Month, int Day, int? Year);

internal sealed record AssistantIntentMatch(
    AssistantIntent Intent,
    string RawName,
    string[] NameTerms,
    string? DayOrStatus,
    AssistantDateReference? DateReference,
    AssistantWeekReference? WeekReference,
    string NormalizedMessage,
    AssistantFilters Filters);

internal sealed record AssistantFilters(string? Operation, string? Company, string? Location, int? Role, string? ShiftTime);
