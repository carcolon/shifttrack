using ShiftTrack.Application;
using ShiftTrack.Tests.Shared.Builders;
using Xunit;

namespace ShiftTrack.Api.Tests;

public sealed class CompanyScopeHelpersTests
{
    [Fact]
    public void IsInCallerCompanyScope_AllowsSystemHiddenAdminAcrossCompanies()
    {
        var superAdmin = new UserBuilder()
            .WithEmail("superadmin@company.com")
            .AsAdmin()
            .SystemHidden()
            .WithCompany("")
            .Build();
        var target = new UserBuilder()
            .WithEmail("employee@otherco.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build();

        Assert.True(CompanyScopeHelpers.IsInCallerCompanyScope(superAdmin, target));
    }

    [Fact]
    public void IsInCallerCompanyScope_AllowsSharedCompanyScope_WhenTargetPrimaryCompanyDiffers()
    {
        var admin = new UserBuilder()
            .WithEmail("admin@companyx.com")
            .AsAdmin()
            .WithCompany("Company X")
            .WithCompanyScope(CompanyScopeHelpers.BuildCompanyScopeJson(new[] { "Company X" }, "Company X"))
            .Build();

        var target = new UserBuilder()
            .WithEmail("employee@companyy.com")
            .AsEmployee()
            .WithCompany("Company Y")
            .WithCompanyScope(CompanyScopeHelpers.BuildCompanyScopeJson(new[] { "Company Y", "Company X" }, "Company Y"))
            .Build();

        Assert.True(CompanyScopeHelpers.IsInCallerCompanyScope(admin, target));
    }

    [Fact]
    public void IsInCallerCompanyScope_BlocksWhenNoCompaniesOverlap()
    {
        var admin = new UserBuilder()
            .WithEmail("admin@companyx.com")
            .AsAdmin()
            .WithCompany("Company X")
            .WithCompanyScope(CompanyScopeHelpers.BuildCompanyScopeJson(new[] { "Company X" }, "Company X"))
            .Build();

        var target = new UserBuilder()
            .WithEmail("employee@companyy.com")
            .AsEmployee()
            .WithCompany("Company Y")
            .WithCompanyScope(CompanyScopeHelpers.BuildCompanyScopeJson(new[] { "Company Y" }, "Company Y"))
            .Build();

        Assert.False(CompanyScopeHelpers.IsInCallerCompanyScope(admin, target));
    }
}
