# DevHunt Quick Deploy Script
# Usage: .\deploy.ps1 [command]
# Commands:
#   deploy   - Full deploy (sync + build + restart)
#   sync     - Only sync files
#   restart  - Only restart services
#   logs     - View logs
#   status   - Check service status
#   ssh      - Open SSH session

param(
    [Parameter(Position=0)]
    [ValidateSet("deploy", "sync", "restart", "logs", "status", "ssh", "build", "help")]
    [string]$Command = "help",

    [string]$EC2Ip = $env:DEVHUNT_EC2_IP,
    [string]$EC2User = $(if ($env:DEVHUNT_EC2_USER) { $env:DEVHUNT_EC2_USER } else { "ubuntu" }),
    [string]$SshKeyPath = $env:DEVHUNT_SSH_KEY_PATH,
    [string]$ProjectPath = $(if ($env:DEVHUNT_PROJECT_PATH) { $env:DEVHUNT_PROJECT_PATH } else { (Resolve-Path "$PSScriptRoot\..\..").Path })
)

# ============================================
# CONFIGURATION - populated from parameters / environment variables
# ============================================
# Required: set these before running the script, e.g.
#   $env:DEVHUNT_EC2_IP = "203.0.113.10"
#   $env:DEVHUNT_SSH_KEY_PATH = "C:\path\to\dev-hunt.pem"
if ($Command -ne "help") {
    if (-not $EC2Ip) {
        Write-Error "EC2 IP is not set. Pass -EC2Ip <ip> or set `$env:DEVHUNT_EC2_IP."
        exit 1
    }
    if (-not $SshKeyPath) {
        Write-Error "SSH key path is not set. Pass -SshKeyPath <path> or set `$env:DEVHUNT_SSH_KEY_PATH."
        exit 1
    }
}

$EC2_IP = $EC2Ip
$EC2_USER = $EC2User
$SSH_KEY = $SshKeyPath
$PROJECT_PATH = $ProjectPath
$REMOTE_PATH = "~/devhunt"

# SSH command helper
$SSH = "C:\Windows\System32\OpenSSH\ssh.exe"
$SCP = "C:\Windows\System32\OpenSSH\scp.exe"

function Write-Color {
    param([string]$text, [string]$color = "White")
    Write-Host $text -ForegroundColor $color
}

function Invoke-SSH {
    param([string]$command)
    & $SSH -i $SSH_KEY -o StrictHostKeyChecking=no -o ServerAliveInterval=30 "$EC2_USER@$EC2_IP" $command
}

function Test-SSHConnection {
    Write-Color "Testing SSH connection..." "Cyan"
    $result = & $SSH -i $SSH_KEY -o StrictHostKeyChecking=no -o ConnectTimeout=10 "$EC2_USER@$EC2_IP" "echo OK" 2>&1
    if ($result -eq "OK") {
        Write-Color "SSH connection OK" "Green"
        return $true
    } else {
        Write-Color "SSH connection failed: $result" "Red"
        return $false
    }
}

function Sync-Project {
    Write-Color "`nSyncing project to EC2..." "Cyan"
    
    # Create archive
    $archivePath = "$env:TEMP\devhunt-deploy.tar.gz"
    Write-Color "  Creating archive..." "Gray"
    
    Push-Location $PROJECT_PATH
    tar --exclude='node_modules' --exclude='.git' --exclude='bin' --exclude='obj' `
        --exclude='__pycache__' --exclude='*.log' --exclude='.next' --exclude='dist' `
        --exclude='.vs' --exclude='.idea' --exclude='devhunt-deploy.tar.gz' `
        -czf $archivePath .
    Pop-Location
    
    $size = [math]::Round((Get-Item $archivePath).Length / 1MB, 2)
    Write-Color "  Archive size: $size MB" "Gray"
    
    # Upload
    Write-Color "  Uploading..." "Gray"
    & $SCP -i $SSH_KEY -o StrictHostKeyChecking=no $archivePath "${EC2_USER}@${EC2_IP}:~/devhunt-deploy.tar.gz"
    
    # Extract on server
    Write-Color "  Extracting on server..." "Gray"
    Invoke-SSH "cd $REMOTE_PATH; tar -xzf ~/devhunt-deploy.tar.gz; rm ~/devhunt-deploy.tar.gz"
    
    # Cleanup local
    Remove-Item $archivePath -ErrorAction SilentlyContinue
    
    Write-Color "Sync complete!" "Green"
}

function Build-Containers {
    Write-Color "`nBuilding containers..." "Cyan"
    Invoke-SSH "cd $REMOTE_PATH; sudo docker compose build --parallel"
    Write-Color "Build complete!" "Green"
}

function Restart-Services {
    param([string]$Service = "")
    
    if ($Service) {
        Write-Color "`nRestarting $Service..." "Cyan"
        Invoke-SSH "cd $REMOTE_PATH; sudo docker compose up -d $Service --force-recreate"
    } else {
        Write-Color "`nRestarting all services..." "Cyan"
        Invoke-SSH "cd $REMOTE_PATH; sudo docker compose up -d"
        
        # Apply HTTP-only nginx config for IP-based access (no SSL)
        Write-Color "`nApplying HTTP-only nginx config..." "Cyan"
        Start-Sleep 5
        Invoke-SSH "cd $REMOTE_PATH; sudo docker cp nginx/nginx-http-only.conf devhunt-api-gateway-nginx:/etc/nginx/conf.d/default.conf; sudo docker exec devhunt-api-gateway-nginx nginx -s reload 2>/dev/null || true"
    }
    Write-Color "Restart complete!" "Green"
}


function Show-Status {
    Write-Color "`nService Status:" "Cyan"
    Invoke-SSH "cd $REMOTE_PATH; sudo docker ps --format 'table {{.Names}}\t{{.Status}}' | sort"
}

function Show-Logs {
    param([string]$Service = "")
    
    if ($Service) {
        Write-Color "`nLogs for ${Service}:" "Cyan"
        Invoke-SSH "cd $REMOTE_PATH; sudo docker compose logs -f --tail 100 $Service"
    } else {
        Write-Color "`nRecent logs (all services):" "Cyan"
        Invoke-SSH "cd $REMOTE_PATH; sudo docker compose logs --tail 50"
    }
}

function Open-SSHSession {
    Write-Color "`nOpening SSH session..." "Cyan"
    & $SSH -i $SSH_KEY -o StrictHostKeyChecking=no "$EC2_USER@$EC2_IP"
}

function Show-Help {
    Write-Color "`nDevHunt Deployment Script" "Cyan"
    Write-Color "================================" "Cyan"
    Write-Host ""
    Write-Host "Usage: .\deploy.ps1 <command>"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  deploy   - Full deployment (sync + build + restart)" -ForegroundColor Yellow
    Write-Host "  sync     - Sync files to server (no restart)" -ForegroundColor Yellow
    Write-Host "  build    - Build Docker images on server" -ForegroundColor Yellow
    Write-Host "  restart  - Restart all services" -ForegroundColor Yellow
    Write-Host "  status   - Show service status" -ForegroundColor Yellow
    Write-Host "  logs     - View logs" -ForegroundColor Yellow
    Write-Host "  ssh      - Open SSH session to server" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\deploy.ps1 deploy          # Full deploy"
    Write-Host "  .\deploy.ps1 restart         # Just restart"
    Write-Host ""
    Write-Color "Server: ${EC2_USER}@${EC2_IP}" "Gray"
}

# ============================================
# MAIN
# ============================================

switch ($Command) {
    "deploy" {
        Write-Color "`nFULL DEPLOYMENT STARTING..." "Magenta"
        Write-Color "==============================" "Magenta"
        
        if (-not (Test-SSHConnection)) { exit 1 }
        
        $startTime = Get-Date
        
        Sync-Project
        Build-Containers
        Restart-Services
        
        Start-Sleep 5
        Show-Status
        
        $duration = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
        Write-Color "`nDEPLOYMENT COMPLETE! (${duration} min)" "Green"
        Write-Color "App: http://$EC2_IP" "Cyan"
    }
    
    "sync" {
        if (-not (Test-SSHConnection)) { exit 1 }
        Sync-Project
    }
    
    "build" {
        if (-not (Test-SSHConnection)) { exit 1 }
        Build-Containers
    }
    
    "restart" {
        if (-not (Test-SSHConnection)) { exit 1 }
        Restart-Services
        Start-Sleep 3
        Show-Status
    }
    
    "status" {
        if (-not (Test-SSHConnection)) { exit 1 }
        Show-Status
    }
    
    "logs" {
        if (-not (Test-SSHConnection)) { exit 1 }
        Show-Logs
    }
    
    "ssh" {
        Open-SSHSession
    }
    
    "help" {
        Show-Help
    }
}
