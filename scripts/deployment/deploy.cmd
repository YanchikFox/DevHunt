@echo off
REM DevHunt Quick Deploy
REM Just double-click to deploy!

echo.
echo ========================================
echo   DevHunt Quick Deploy to EC2
echo ========================================
echo.

cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "deploy.ps1" %*

if "%1"=="" (
    echo.
    echo Usage: deploy [command]
    echo Commands: deploy, sync, restart, status, logs, ssh
    echo.
    pause
)
