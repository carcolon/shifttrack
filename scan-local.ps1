param(
    [switch]$SkipSemgrep,
    [switch]$SkipDotnetVuln,
    [switch]$SkipNpmAudit,
    [switch]$SkipSecretScan,
    [switch]$UseOwaspPack,
    [switch]$UseSemgrepCi,
    [switch]$SkipSeveritySummary
)

$ErrorActionPreference = "Continue"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendPath = Join-Path $root "shifttrack-api"
$frontendPath = Join-Path $root "shifttrack-web"
$semgrepConfig = Join-Path $root "semgrep.yml"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportDir = Join-Path $root ("reports\" + $timestamp)

New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

$summary = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$errors = [System.Collections.Generic.List[string]]::new()

function Set-LatestReportCopy {
    param(
        [string]$SourcePath,
        [string]$TargetName
    )

    if (Test-Path $SourcePath) {
        Copy-Item -Path $SourcePath -Destination (Join-Path $root $TargetName) -Force
        Copy-Item -Path $SourcePath -Destination (Join-Path $root "reports\$TargetName") -Force
    }
}

function Get-JsonFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try {
        return Get-Content $Path -Raw | ConvertFrom-Json
    } catch {
        $warnings.Add("Could not parse JSON file: $Path")
        return $null
    }
}

function Add-SeverityBucket {
    param(
        [hashtable]$Bucket,
        [string]$Severity,
        [int]$Count = 1
    )

    if (-not $Bucket.ContainsKey($Severity)) {
        $Bucket[$Severity] = 0
    }

    $Bucket[$Severity] += $Count
}

function Get-IntOrZero {
    param($Value)
    if ($null -eq $Value) { return 0 }
    try {
        return [int]$Value
    } catch {
        return 0
    }
}

function Test-CommandAvailable {
    param([string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            $errors.Add("$Name failed with exit code $LASTEXITCODE")
            Write-Host "FAILED: $Name (exit $LASTEXITCODE)" -ForegroundColor Red
        } else {
            $summary.Add($Name)
            Write-Host "OK: $Name" -ForegroundColor Green
        }
    } catch {
        $errors.Add("$Name failed: $($_.Exception.Message)")
        Write-Host "FAILED: $Name" -ForegroundColor Red
    }
}

if (-not (Test-Path $backendPath)) { throw "Backend folder not found: $backendPath" }
if (-not (Test-Path $frontendPath)) { throw "Frontend folder not found: $frontendPath" }

if (-not $SkipSemgrep) {
    if (-not (Test-CommandAvailable "semgrep")) {
        $errors.Add("semgrep is not installed or not in PATH.")
    } else {
        if (-not (Test-Path $semgrepConfig)) {
            $errors.Add("semgrep.yml not found at repo root: $semgrepConfig")
        } else {
            Run-Step "Semgrep custom rules (JSON)" {
                semgrep --config $semgrepConfig $backendPath $frontendPath --json --output (Join-Path $reportDir "semgrep-custom.json")
            }
            Run-Step "Semgrep custom rules (SARIF)" {
                semgrep --config $semgrepConfig $backendPath $frontendPath --sarif --output (Join-Path $reportDir "semgrep-custom.sarif")
            }
            if ($UseOwaspPack) {
                Run-Step "Semgrep OWASP pack (JSON)" {
                    semgrep --config p/owasp-top-ten $backendPath $frontendPath --json --output (Join-Path $reportDir "semgrep-owasp.json")
                }
            }
            if ($UseSemgrepCi) {
                Run-Step "Semgrep CI rules (JSON)" {
                    semgrep ci --json --output (Join-Path $reportDir "semgrep-ci.json")
                }
            }

            Set-LatestReportCopy -SourcePath (Join-Path $reportDir "semgrep-custom.json") -TargetName "latest-semgrep-custom.json"
            Set-LatestReportCopy -SourcePath (Join-Path $reportDir "semgrep-custom.sarif") -TargetName "latest-semgrep-custom.sarif"
        }
    }
}

if (-not $SkipDotnetVuln) {
    if (-not (Test-CommandAvailable "dotnet")) {
        $errors.Add("dotnet is not installed or not in PATH.")
    } else {
        Run-Step ".NET vulnerable packages (API)" {
            Push-Location (Join-Path $backendPath "ShiftTrack.Api")
            dotnet list package --vulnerable --include-transitive | Tee-Object -FilePath (Join-Path $reportDir "dotnet-vulnerable-api.txt")
            Pop-Location
        }
        Run-Step ".NET vulnerable packages JSON (API)" {
            Push-Location (Join-Path $backendPath "ShiftTrack.Api")
            dotnet list package --vulnerable --include-transitive --format json | Out-File -FilePath (Join-Path $reportDir "dotnet-vulnerable-api.json") -Encoding utf8
            Pop-Location
        }
    }
}

if (-not $SkipNpmAudit) {
    if (-not (Test-CommandAvailable "npm")) {
        $errors.Add("npm is not installed or not in PATH.")
    } else {
        Run-Step "npm audit (web)" {
            Push-Location $frontendPath
            npm audit --audit-level=high --json | Tee-Object -FilePath (Join-Path $reportDir "npm-audit-web.json") | Out-Null
            # npm audit returns non-zero when vulnerabilities are found.
            $global:LASTEXITCODE = 0
            Pop-Location
        }
    }
}

if (-not $SkipSecretScan) {
    if (-not (Test-CommandAvailable "gitleaks")) {
        $warnings.Add("gitleaks not installed; secret scanning skipped.")
    } else {
        Run-Step "Secret scan (gitleaks)" {
            gitleaks detect --source $root --report-format json --report-path (Join-Path $reportDir "gitleaks.json")
        }
    }
}

$summaryFile = Join-Path $reportDir "summary.txt"
"Scan finished: $(Get-Date -Format s)" | Out-File -FilePath $summaryFile -Encoding UTF8
"Root: $root" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
"" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
"Completed steps:" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
if ($summary.Count -eq 0) {
    " - none" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
} else {
    $summary | ForEach-Object { " - $_" | Out-File -FilePath $summaryFile -Append -Encoding UTF8 }
}
"" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
"Warnings:" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
if ($warnings.Count -eq 0) {
    " - none" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
} else {
    $warnings | ForEach-Object { " - $_" | Out-File -FilePath $summaryFile -Append -Encoding UTF8 }
}
"" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
"Errors / missing tools:" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
if ($errors.Count -eq 0) {
    " - none" | Out-File -FilePath $summaryFile -Append -Encoding UTF8
} else {
    $errors | ForEach-Object { " - $_" | Out-File -FilePath $summaryFile -Append -Encoding UTF8 }
}

if (-not $SkipSeveritySummary) {
    $severityBuckets = @{
        critical = 0
        high = 0
        medium = 0
        low = 0
    }

    $semgrepJsonPath = Join-Path $reportDir "semgrep-custom.json"
    $semgrepJson = Get-JsonFile -Path $semgrepJsonPath
    if ($null -ne $semgrepJson -and $null -ne $semgrepJson.results) {
        foreach ($result in $semgrepJson.results) {
            $sev = ($result.extra.severity | ForEach-Object { $_.ToString().Trim().ToUpperInvariant() })
            switch ($sev) {
                "ERROR" { Add-SeverityBucket -Bucket $severityBuckets -Severity "high" }
                "WARNING" { Add-SeverityBucket -Bucket $severityBuckets -Severity "medium" }
                "INFO" { Add-SeverityBucket -Bucket $severityBuckets -Severity "low" }
                default { Add-SeverityBucket -Bucket $severityBuckets -Severity "low" }
            }
        }
    }

    $npmAuditPath = Join-Path $reportDir "npm-audit-web.json"
    $npmAuditJson = Get-JsonFile -Path $npmAuditPath
    if ($null -ne $npmAuditJson -and $null -ne $npmAuditJson.metadata -and $null -ne $npmAuditJson.metadata.vulnerabilities) {
        Add-SeverityBucket -Bucket $severityBuckets -Severity "critical" -Count (Get-IntOrZero $npmAuditJson.metadata.vulnerabilities.critical)
        Add-SeverityBucket -Bucket $severityBuckets -Severity "high" -Count (Get-IntOrZero $npmAuditJson.metadata.vulnerabilities.high)
        Add-SeverityBucket -Bucket $severityBuckets -Severity "medium" -Count (Get-IntOrZero $npmAuditJson.metadata.vulnerabilities.moderate)
        Add-SeverityBucket -Bucket $severityBuckets -Severity "low" -Count (Get-IntOrZero $npmAuditJson.metadata.vulnerabilities.low)
    }

    $severitySummaryFile = Join-Path $reportDir "severity-summary.txt"
    "Severity summary: $(Get-Date -Format s)" | Out-File -FilePath $severitySummaryFile -Encoding UTF8
    "critical=$($severityBuckets.critical)" | Out-File -FilePath $severitySummaryFile -Append -Encoding UTF8
    "high=$($severityBuckets.high)" | Out-File -FilePath $severitySummaryFile -Append -Encoding UTF8
    "medium=$($severityBuckets.medium)" | Out-File -FilePath $severitySummaryFile -Append -Encoding UTF8
    "low=$($severityBuckets.low)" | Out-File -FilePath $severitySummaryFile -Append -Encoding UTF8

    $severityJsonPath = Join-Path $reportDir "severity-summary.json"
    [pscustomobject]@{
        critical = $severityBuckets.critical
        high = $severityBuckets.high
        medium = $severityBuckets.medium
        low = $severityBuckets.low
        sources = @(
            if (Test-Path $semgrepJsonPath) { "semgrep-custom.json" }
            if (Test-Path $npmAuditPath) { "npm-audit-web.json" }
        ) | Where-Object { $_ }
    } | ConvertTo-Json | Out-File -FilePath $severityJsonPath -Encoding utf8

    Set-LatestReportCopy -SourcePath $severitySummaryFile -TargetName "latest-severity-summary.txt"
    Set-LatestReportCopy -SourcePath $severityJsonPath -TargetName "latest-severity-summary.json"
}

Write-Host ""
Write-Host "Reports directory: $reportDir" -ForegroundColor Yellow
Write-Host "Summary file: $summaryFile" -ForegroundColor Yellow
if (-not $SkipSeveritySummary) {
    Write-Host "Severity summary: $(Join-Path $reportDir "severity-summary.txt")" -ForegroundColor Yellow
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Some checks failed or tools are missing:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

exit 0
