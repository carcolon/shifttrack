using System.Text.Json;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application;

public static class CompanyScopeHelpers
{
    public static string[] ResolveCompanies(User user) =>
        ResolveCompanies(user.CompanyScope, user.Company);

    public static string[] ResolveCompanies(string? companyScope, string? fallbackCompany)
    {
        var companies = new List<string>();

        if (!string.IsNullOrWhiteSpace(companyScope))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(companyScope);
                if (parsed is not null) companies.AddRange(parsed);
            }
            catch (JsonException)
            {
                companies.AddRange(companyScope.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackCompany)) companies.Add(fallbackCompany);

        return companies
            .Select(NormalizeCompany)
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string BuildCompanyScopeJson(IEnumerable<string>? companies, string primaryCompany)
    {
        var resolved = (companies ?? Array.Empty<string>())
            .Append(primaryCompany)
            .Select(NormalizeCompany)
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(resolved);
    }

    public static bool IsInCallerCompanyScope(User callerUser, User targetUser)
    {
        if (callerUser.IsSystemHidden) return true;

        var callerCompanies = ResolveCompanies(callerUser);
        var targetCompanies = ResolveCompanies(targetUser);
        return callerCompanies.Any(company => targetCompanies.Contains(company, StringComparer.OrdinalIgnoreCase));
    }

    public static bool CanAssignCompanies(User callerUser, string targetCompany, IEnumerable<string>? targetCompanies)
    {
        if (callerUser.IsSystemHidden) return true;

        var callerCompanies = ResolveCompanies(callerUser);
        if (callerCompanies.Length == 0) return false;

        var requestedCompanies = (targetCompanies ?? Array.Empty<string>())
            .Append(targetCompany)
            .Select(NormalizeCompany)
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return requestedCompanies.All(company => callerCompanies.Contains(company, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeCompany(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
