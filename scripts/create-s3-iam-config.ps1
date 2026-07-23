# Create minimal IAM config for SeaweedFS that matches our credentials
$accessKey = $env:OBJECT_STORAGE_ACCESS_KEY
if (-not $accessKey) {
    $accessKey = "devhunt_access_key_dev_123456789012"
}

$secretKey = $env:OBJECT_STORAGE_SECRET_KEY
if (-not $secretKey) {
    $secretKey = "devhunt_secret_key_dev_1234567890123456"
}

Write-Host "Creating IAM config with AccessKey: $accessKey"

$iamConfig = @{
    identities = @(
        @{
            name = "devhunt"
            credentials = @(
                @{
                    accessKey = $accessKey
                    secretKey = $secretKey
                }
            )
            actions = @("Admin", "Read", "Write", "List", "Tagging")
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "IAM Config JSON:"
Write-Host $iamConfig

# Persist config to the bind-mounted file (seaweedfs/s3.json)
$repoRoot = Split-Path -Parent $PSScriptRoot
$targetPath = Join-Path $repoRoot "seaweedfs\s3.json"

Write-Host "`nWriting config to $targetPath ..."
Set-Content -Path $targetPath -Value $iamConfig -Encoding UTF8
Write-Host "Done."

try {
    Write-Host "`nRestarting SeaweedFS to pick up the new config..."
    Push-Location $repoRoot
    docker-compose restart object-storage | Out-Host
    Pop-Location
    
    Write-Host "`nWaiting for SeaweedFS to start..."
    Start-Sleep -Seconds 5
    Write-Host "SeaweedFS restarted successfully!"
} catch {
    Write-Warning "Could not restart SeaweedFS automatically. Please run 'docker-compose restart object-storage' manually."
    Write-Warning $_
    try { Pop-Location | Out-Null } catch {}
}

Write-Host "`nTo verify, run: docker exec storage-seaweedfs sh -c 'echo \"fs.cat /etc/seaweedfs/s3.json\" | weed shell'"
