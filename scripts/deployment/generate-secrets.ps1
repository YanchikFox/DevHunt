# Generate secrets for .env file
# This script generates all required secrets for production deployment

function Generate-RandomString {
    param([int]$Length = 32)
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*"
    $random = 1..$Length | ForEach-Object { Get-Random -Maximum $chars.length }
    return -join ($random | ForEach-Object { $chars[$_] })
}

function Generate-Base64String {
    param([int]$Length = 32)
    $bytes = New-Object byte[] $Length
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes)
}

Write-Host "🔐 Generating secrets for .env file..." -ForegroundColor Cyan

# Read env.example
$envExample = Get-Content "env.example" -Raw

# Generate secrets
$secrets = @{
    POSTGRES_PASSWORD = Generate-Base64String -Length 32
    RABBITMQ_DEFAULT_PASS = Generate-Base64String -Length 32
    JWT_KEY = Generate-Base64String -Length 32
    ENCRYPTION_KEY = Generate-Base64String -Length 24
    ENCRYPTION_IV = Generate-Base64String -Length 12
    OBJECT_STORAGE_ACCESS_KEY = Generate-Base64String -Length 32
    OBJECT_STORAGE_SECRET_KEY = Generate-Base64String -Length 32
    OPENSEARCH_INITIAL_ADMIN_PASSWORD = Generate-Base64String -Length 32
}

# Replace empty values in env.example
$envContent = $envExample
foreach ($key in $secrets.Keys) {
    $pattern = "${key}=$"
    $replacement = "${key}=$($secrets[$key])"
    $envContent = $envContent -replace $pattern, $replacement
}

# Write .env file
$envContent | Out-File -FilePath ".env" -Encoding utf8 -NoNewline

Write-Host "✓ .env file created with generated secrets" -ForegroundColor Green
Write-Host ""
Write-Host "Generated secrets (keep them safe!):" -ForegroundColor Yellow
foreach ($key in $secrets.Keys) {
    Write-Host "  $key = $($secrets[$key])" -ForegroundColor Gray
}


