# Collect CI workflow results into a single file for easy sharing
# Usage: .\scripts\collect-ci-logs.ps1
#        .\scripts\collect-ci-logs.ps1 -Branch feature/my-branch
#        .\scripts\collect-ci-logs.ps1 -FailedOnly

param(
    [string]$Branch = "",
    [string]$OutputFile = "ci-results.txt",
    [switch]$FailedOnly,
    [int]$Limit = 30
)

$ErrorActionPreference = "Continue"

# Auto-detect branch if not specified
if (-not $Branch) {
    $Branch = git branch --show-current 2>$null
    if (-not $Branch) { $Branch = "main" }
}

Write-Host "Collecting CI results for branch: $Branch ..." -ForegroundColor Cyan

$runs = gh run list --branch $Branch --limit $Limit --json "databaseId,name,status,conclusion,createdAt,event,headSha" 2>&1 | ConvertFrom-Json

if (-not $runs -or $runs.Count -eq 0) {
    Write-Host "No workflow runs found for branch '$Branch'" -ForegroundColor Yellow
    exit 0
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("=== CI RESULTS ===")
[void]$sb.AppendLine("Branch: $Branch")
[void]$sb.AppendLine("Collected: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("Total runs: $($runs.Count)")
[void]$sb.AppendLine("")

# Group by workflow name, take latest run of each
$latest = @{}
foreach ($run in $runs) {
    if (-not $latest.ContainsKey($run.name)) {
        $latest[$run.name] = $run
    }
}

$failCount = 0
$successCount = 0

foreach ($entry in $latest.GetEnumerator() | Sort-Object { $_.Value.createdAt } -Descending) {
    $run = $entry.Value
    $status = if ($run.conclusion) { $run.conclusion } else { $run.status }

    if ($FailedOnly -and $status -eq "success") { continue }

    $icon = switch ($status) {
        "success"   { "[OK]" }
        "failure"   { "[FAIL]" }
        "cancelled" { "[SKIP]" }
        default     { "[....]" }
    }

    if ($status -eq "failure") { $failCount++ } elseif ($status -eq "success") { $successCount++ }

    [void]$sb.AppendLine("$icon $($run.name)")
    [void]$sb.AppendLine("     Status: $status | SHA: $($run.headSha.Substring(0,7)) | $($run.createdAt)")

    # For failed runs, fetch the failed step logs
    if ($status -eq "failure") {
        [void]$sb.AppendLine("     --- FAILED LOGS (last 80 lines) ---")
        try {
            $logs = gh run view $run.databaseId --log-failed 2>&1 | Select-Object -Last 80
            foreach ($line in $logs) {
                [void]$sb.AppendLine("     $line")
            }
        } catch {
            [void]$sb.AppendLine("     (could not fetch logs)")
        }
        [void]$sb.AppendLine("     --- END LOGS ---")
    }

    [void]$sb.AppendLine("")
}

# Summary at the top
$summary = "Summary: $successCount passed, $failCount failed, $($latest.Count - $successCount - $failCount) other"
[void]$sb.Insert($sb.ToString().IndexOf("Total runs:"), "")

$result = $sb.ToString()
$result | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "$successCount passed, $failCount failed, $($latest.Count - $successCount - $failCount) other" -ForegroundColor $(if ($failCount -gt 0) { "Yellow" } else { "Green" })
Write-Host ""
Write-Host "Results saved to: $OutputFile" -ForegroundColor Green
Write-Host "Share this file with Claude for analysis." -ForegroundColor Gray
