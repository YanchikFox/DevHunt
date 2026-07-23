<#
Setting up BuildKit globally (чтобы не вводить каждый раз)
#>
Write-Host "🔧 Настройка BuildKit для Docker..." -ForegroundColor Cyan

# Для текущей сессии PowerShell
$env:DOCKER_BUILDKIT = "1"
$env:COMPOSE_DOCKER_CLI_BUILD = "1"

Write-Host "✅ BuildKit включен для текущей сессии PowerShell" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Чтобы включить BuildKit постоянно, добавьте в ваш PowerShell профиль:" -ForegroundColor Yellow
Write-Host "   `$env:DOCKER_BUILDKIT = '1'" -ForegroundColor Gray
Write-Host "   `$env:COMPOSE_DOCKER_CLI_BUILD = '1'" -ForegroundColor Gray
Write-Host ""
Write-Host "Или используйте скрипты dev-restart.ps1 и dev-rebuild.ps1 - они автоматически включают BuildKit" -ForegroundColor Cyan

