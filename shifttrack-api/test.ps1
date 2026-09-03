param(
    [Parameter(Position = 0)]
    [ValidateSet("unit", "integration", "acceptance", "all")]
    [string]$Suite = "all"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$commands = @{
    unit = @("dotnet", "test", "tests\Unit\ShiftTrack.Api.Tests\ShiftTrack.Api.Tests.csproj", "-p:UseAppHost=false")
    integration = @("dotnet", "test", "tests\Integration\ShiftTrack.Api.IntegrationTests\ShiftTrack.Api.IntegrationTests.csproj", "-p:UseAppHost=false")
    acceptance = @("dotnet", "test", "tests\Acceptance\ShiftTrack.Api.AcceptanceTests\ShiftTrack.Api.AcceptanceTests.csproj", "-p:UseAppHost=false")
}

$executionOrder = if ($Suite -eq "all")
{
    @("unit", "integration", "acceptance")
}
else
{
    @($Suite)
}

Push-Location $root
try
{
    foreach ($item in $executionOrder)
    {
        Write-Host "Running $item tests..." -ForegroundColor Cyan
        & $commands[$item][0] $commands[$item][1] $commands[$item][2] $commands[$item][3]
        if ($LASTEXITCODE -ne 0)
        {
            throw "The $item test suite failed."
        }
    }
}
finally
{
    Pop-Location
}
