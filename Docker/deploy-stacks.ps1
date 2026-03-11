#Requires -Version 5.1
<#
.SYNOPSIS
    DeepResearch Docker Compose Stack Manager
    Unified deployment automation for 4-stack architecture

.DESCRIPTION
    Manages the DeepResearch 4-stack unified architecture (Core, AI, Websearch, Monitoring)
    Provides commands to start, stop, restart, status, and monitor all stacks

.PARAMETER Action
    Operation to perform: start, stop, restart, status, logs, health, validate, cleanup

.PARAMETER Stack
    Which stack(s) to target: all, core, ai, websearch, monitoring

.PARAMETER Follow
    For logs action: continuously follow logs

.PARAMETER Tail
    Number of log lines to show (default: 50)

.PARAMETER CreateMissing
    Create placeholder docker-compose files if they don't exist

.EXAMPLE
    .\deploy-stacks.ps1 -Action start -Stack all
    Starts all stacks in proper dependency order

.EXAMPLE
    .\deploy-stacks.ps1 -Action status -Stack all
    Shows status of all running containers

.EXAMPLE
    .\deploy-stacks.ps1 -Action logs -Stack core -Follow
    Follows logs from core stack services

.EXAMPLE
    .\deploy-stacks.ps1 -Action start -Stack all -CreateMissing
    Creates placeholder docker-compose files if missing, then starts stacks

.NOTES
    Author: DeepResearch Architecture Team
    Version: 1.0
    Date: March 2, 2026
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "health", "validate", "cleanup")]
    [string]$Action = "status",

    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "core", "ai", "websearch", "monitoring")]
    [string]$Stack = "all",

    [Parameter(Mandatory=$false)]
    [switch]$Follow,

    [Parameter(Mandatory=$false)]
    [int]$Tail = 50,

    [Parameter(Mandatory=$false)]
    [switch]$CreateMissing
)

# ═══════════════════════════════════════════════════════════════════════════════
# CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"
$WarningPreference = "Continue"

# Colors for output
$Colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
    Header = "Magenta"
}

# Stack definitions
$Stacks = @{
    "core" = @{
        "name" = "Core Services"
        "compose_file" = "Docker/docker-compose.core.yml"
        "services" = @("deepresearch-api", "deep-research-agent", "redis", "influxdb", "redis-exporter")
        "depends_on" = @()
        "health_endpoint" = "http://localhost:5000/health"
    }
    "ai" = @{
        "name" = "AI Services"
        "compose_file" = "Docker/docker-compose.ai.yml"
        "services" = @("ollama", "qdrant", "lightning-server")
        "depends_on" = @("core")
        "health_endpoint" = "http://localhost:11434/api/health"
    }
    "websearch" = @{
        "name" = "Websearch Services"
        "compose_file" = "Docker/docker-compose.websearch.yml"
        "services" = @("caddy", "searxng", "crawl4ai")
        "depends_on" = @("core")
        "health_endpoint" = "http://localhost:80"
    }
    "monitoring" = @{
        "name" = "Monitoring Stack"
        "compose_file" = "Docker/Observability/docker-compose-monitoring.yml"
        "services" = @("prometheus", "grafana", "alertmanager", "jaeger", "otel-collector")
        "depends_on" = @("core", "ai", "websearch")
        "health_endpoint" = "http://localhost:9090/-/healthy"
    }
}

$NetworkName = "deepresearch-hub"

# Calculate project root based on script location (like bash version)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir  # Parent of Docker/ directory

# Validate project structure
if (-not (Test-Path (Join-Path $ProjectRoot "Docker") -PathType Container)) {
    Write-Status "ERROR: Invalid project structure detected" "Error"
    Write-Host "  Script location: $ScriptDir" -ForegroundColor Yellow
    Write-Host "  Detected root: $ProjectRoot" -ForegroundColor Yellow
    exit 1
}

Write-Verbose "Project Root: $ProjectRoot"

# ═══════════════════════════════════════════════════════════════════════════════
# UTILITY FUNCTIONS
# ═══════════════════════════════════════════════════════════════════════════════

function Write-Status {
    param(
        [string]$Message,
        [string]$Type = "Info"
    )

    $color = $Colors[$Type]
    $prefix = switch($Type) {
        "Success" { "[OK]" }
        "Error" { "[ERROR]" }
        "Warning" { "[WARN]" }
        "Info" { "[INFO]" }
        "Header" { "==" }
        default { "*" }
    }

    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "+====================================================================+" -ForegroundColor Magenta
    Write-Host "| $($Title.PadRight(63)) |" -ForegroundColor Magenta
    Write-Host "+====================================================================+" -ForegroundColor Magenta
    Write-Host ""
}

function Test-ComposeFileExists {
    param([string]$FilePath)
    return Test-Path -Path $FilePath -PathType Leaf
}

function Test-AllComposeFilesExist {
    param([string[]]$StackFilter)

    $stacksToCheck = if ($StackFilter -contains "all") { @("core", "ai", "websearch", "monitoring") } else { $StackFilter }
    $missingFiles = @()

    foreach ($stackName in $stacksToCheck) {
        $stack = $Stacks[$stackName]
        $filePath = Join-Path -Path $ProjectRoot -ChildPath $stack.compose_file

        if (-not (Test-ComposeFileExists -FilePath $filePath)) {
            $missingFiles += @{
                Stack = $stackName
                File = $stack.compose_file
                FullPath = $filePath
            }
        }
    }

    return $missingFiles
}

function Test-NetworkExists {
    $network = docker network ls --filter "name=$NetworkName" --format "{{.Name}}" 2>$null
    return $network -eq $NetworkName
}

function Show-MissingFilesHelp {
    param([array]$MissingFiles)

    Write-Host ""
    Write-Host "+====================================================================+" -ForegroundColor Yellow
    Write-Host "|                    MISSING DOCKER COMPOSE FILES                |" -ForegroundColor Yellow
    Write-Host "+====================================================================+" -ForegroundColor Yellow
    Write-Host ""

    foreach ($missing in $MissingFiles) {
        Write-Status "Missing: $($missing.File)" "Error"
        Write-Host "  Path: $($missing.FullPath)" -ForegroundColor DarkGray
    }

    Write-Host ""
    Write-Host "[WARN]  The following docker-compose files are required but not found:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Option 1: Provide the missing docker-compose files in the Docker directory" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Option 2: Create placeholder files with this script:" -ForegroundColor Cyan
    Write-Host "    ./deploy-stacks.ps1 -Action start -CreateMissing" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Option 3: Generate files manually (examples available in Docker/examples/)" -ForegroundColor Cyan
    Write-Host ""
}

function Start-InteractiveFileCreation {
    param([array]$MissingFiles)

    Write-Host ""
    Write-Host "Would you like to create placeholder docker-compose files? (y/n)" -ForegroundColor Cyan
    $response = Read-Host

    if ($response -eq "y" -or $response -eq "yes") {
        foreach ($missing in $MissingFiles) {
            $dir = Split-Path -Path $missing.FullPath

            if (-not (Test-Path -Path $dir -PathType Container)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Write-Status "Created directory: $dir" "Info"
            }

            # Create a basic placeholder compose file
            $content = @"
version: '3.8'

services:
  # Placeholder for $($missing.Stack) stack
  # TODO: Add service definitions

networks:
  deepresearch-hub:
    external: true
    driver: bridge
"@

            Set-Content -Path $missing.FullPath -Value $content
            Write-Status "Created placeholder: $($missing.File)" "Success"
        }

        Write-Host ""
        Write-Status "Placeholder files created. Please edit them with proper service definitions." "Warning"
        Write-Host ""
        return $true
    }

    return $false
}

function Create-Network {
    if (-not (Test-NetworkExists)) {
        Write-Status "Creating unified network: $NetworkName" "Info"
        try {
            docker network create $NetworkName --driver bridge | Out-Null
            Write-Status "Network created successfully" "Success"
            return $true
        } catch {
            Write-Status "Failed to create network: $_" "Error"
            return $false
        }
    } else {
        Write-Status "Network already exists: $NetworkName" "Info"
        return $true
    }
}

function Get-StacksToProcess {
    param([string]$StackFilter)
    
    if ($StackFilter -eq "all") {
        return @("core", "ai", "websearch", "monitoring")
    } else {
        return @($StackFilter)
    }
}

function Get-SortedStacks {
    param(
        [string[]]$StackList,
        [bool]$ReverseOrder = $false
    )
    
    # Define dependency order
    $order = @("core", "ai", "websearch", "monitoring")
    
    $sorted = $StackList | Sort-Object { $order.IndexOf($_) }
    
    if ($ReverseOrder) {
        $sorted = $sorted | Sort-Object { $order.IndexOf($_) } -Descending
    }
    
    return $sorted
}

function Start-Stack {
    param([string]$StackName)
    
    $stack = $Stacks[$StackName]
    Write-Status "Starting $($stack.name)..." "Info"
    
    try {
        Push-Location $ProjectRoot
        docker-compose -f $stack.compose_file up -d 2>&1 | Out-Null
        Write-Status "$($stack.name) started" "Success"
        Pop-Location
        return $true
    } catch {
        Write-Status "Failed to start $($stack.name): $_" "Error"
        Pop-Location
        return $false
    }
}

function Stop-Stack {
    param([string]$StackName)
    
    $stack = $Stacks[$StackName]
    Write-Status "Stopping $($stack.name)..." "Info"
    
    try {
        Push-Location $ProjectRoot
        docker-compose -f $stack.compose_file down 2>&1 | Out-Null
        Write-Status "$($stack.name) stopped" "Success"
        Pop-Location
        return $true
    } catch {
        Write-Status "Failed to stop $($stack.name): $_" "Error"
        Pop-Location
        return $false
    }
}

function Restart-Stack {
    param([string]$StackName)
    
    if (Stop-Stack $StackName) {
        Start-Sleep -Seconds 3
        return Start-Stack $StackName
    }
    return $false
}

function Get-StackStatus {
    param([string]$StackName)
    
    $stack = $Stacks[$StackName]
    
    Write-Status $stack.name "Header"
    
    foreach ($service in $stack.services) {
        $container = docker ps -a --filter "name=$service" --format "{{.Names}}\t{{.Status}}" 2>$null
        
        if ($container) {
            $parts = $container -split '\s+' | Select-Object -First 2
            $name = $parts[0]
            $status = $parts[1..99] -join ' '
            
            if ($status -like "*Up*") {
                Write-Status "$name → $status" "Success"
            } elseif ($status -like "*Exited*") {
                Write-Status "$name → $status" "Error"
            } else {
                Write-Status "$name → $status" "Warning"
            }
        } else {
            Write-Status "$service → NOT RUNNING" "Error"
        }
    }
}

function Get-StackHealth {
    param([string]$StackName)
    
    $stack = $Stacks[$StackName]
    
    Write-Status $stack.name "Header"
    
    $healthyCount = 0
    $totalCount = 0
    
    foreach ($service in $stack.services) {
        $totalCount++
        $container = docker ps --filter "name=$service" --format "{{.Names}}\t{{.Status}}" 2>$null
        
        if ($container) {
            if ($container -like "*healthy*") {
                Write-Status "$service → HEALTHY" "Success"
                $healthyCount++
            } elseif ($container -like "*unhealthy*") {
                Write-Status "$service → UNHEALTHY" "Warning"
            } elseif ($container -like "*health: starting*") {
                Write-Status "$service → STARTING" "Info"
            } else {
                Write-Status "$service → UP (no health check)" "Info"
                $healthyCount++
            }
        } else {
            Write-Status "$service → NOT RUNNING" "Error"
        }
    }
    
    Write-Status "Health: $healthyCount/$totalCount services" $(if($healthyCount -eq $totalCount) { "Success" } else { "Warning" })
}

function Show-StackLogs {
    param(
        [string]$StackName,
        [bool]$Follow = $false,
        [int]$Tail = 50
    )
    
    $stack = $Stacks[$StackName]
    
    Write-Status "Showing logs for $($stack.name)" "Info"
    Write-Host ""
    
    try {
        Push-Location $ProjectRoot
        
        if ($Follow) {
            docker-compose -f $stack.compose_file logs -f --tail=$Tail
        } else {
            docker-compose -f $stack.compose_file logs --tail=$Tail
        }
        
        Pop-Location
    } catch {
        Write-Status "Failed to retrieve logs: $_" "Error"
        Pop-Location
    }
}

function Validate-Deployment {
    Write-Header "Validating Deployment"
    
    # Check Docker daemon
    Write-Status "Checking Docker daemon..." "Info"
    try {
        docker version | Out-Null
        Write-Status "Docker daemon is running" "Success"
    } catch {
        Write-Status "Docker daemon is not running" "Error"
        return $false
    }
    
    # Check network
    Write-Status "Checking unified network..." "Info"
    if (Test-NetworkExists) {
        Write-Status "Network $NetworkName exists" "Success"
    } else {
        Write-Status "Network $NetworkName not found" "Error"
        return $false
    }
    
    # Check all services
    Write-Status "Checking all services..." "Info"
    $allServices = $Stacks.Values.services | Sort-Object -Unique
    $runningCount = 0
    
    foreach ($service in $allServices) {
        $container = docker ps --filter "name=$service" --format "{{.Names}}" 2>$null
        if ($container) {
            $runningCount++
        }
    }
    
    Write-Status "$runningCount/$($allServices.Count) services running" $(if($runningCount -eq $allServices.Count) { "Success" } else { "Warning" })
    
    # Check critical ports
    Write-Status "Checking critical ports..." "Info"
    $ports = @{
        "5000" = "API"
        "6379" = "Redis"
        "8086" = "InfluxDB"
        "9090" = "Prometheus"
        "3001" = "Grafana"
        "11434" = "Ollama"
        "6333" = "Qdrant"
    }
    
    $openPorts = 0
    foreach ($port in $ports.Keys) {
        $connection = Test-NetConnection -ComputerName localhost -Port $port -ErrorAction SilentlyContinue
        if ($connection.TcpTestSucceeded) {
            Write-Status "Port $port ($($ports[$port])) is open" "Success"
            $openPorts++
        }
    }
    
    Write-Status "$openPorts/$($ports.Count) critical ports accessible" $(if($openPorts -ge 5) { "Success" } else { "Warning" })
    
    return $true
}

function Cleanup-Stack {
    Write-Header "Cleaning Up DeepResearch Stack"
    
    $proceed = Read-Host "This will remove all containers, volumes, and the network. Continue? (yes/no)"
    
    if ($proceed -ne "yes") {
        Write-Status "Cleanup cancelled" "Warning"
        return
    }
    
    # Stop and remove all stacks (reverse order)
    $sortedStacks = Get-SortedStacks (Get-StacksToProcess "all") -ReverseOrder $true
    
    foreach ($stackName in $sortedStacks) {
        Stop-Stack $stackName
        Start-Sleep -Seconds 2
    }
    
    # Remove network
    if (Test-NetworkExists) {
        Write-Status "Removing network: $NetworkName" "Info"
        docker network rm $NetworkName 2>$null
        Write-Status "Network removed" "Success"
    }
    
    Write-Status "Cleanup completed" "Success"
}

# ═══════════════════════════════════════════════════════════════════════════════
# MAIN EXECUTION
# ═══════════════════════════════════════════════════════════════════════════════

Write-Header "DeepResearch Stack Manager v1.0"

# Validate we're in the correct directory
if (-not (Test-Path (Join-Path $ProjectRoot "Docker") -PathType Container)) {
    Write-Status "ERROR: Could not detect project root. Please run from workspace root or Docker directory." "Error"
    exit 1
}

# Check for missing compose files
$missingFiles = Test-AllComposeFilesExist -StackFilter @($Stack)
if ($missingFiles.Count -gt 0) {
    Show-MissingFilesHelp -MissingFiles $missingFiles

    if ($CreateMissing) {
        if (Start-InteractiveFileCreation -MissingFiles $missingFiles) {
            Write-Status "Please edit the placeholder files and run the script again" "Warning"
            exit 0
        }
    }

    Write-Status "Cannot proceed without docker-compose files" "Error"
    Write-Host "  Use -CreateMissing flag to create placeholder files:" -ForegroundColor Cyan
    Write-Host "  ./deploy-stacks.ps1 -Action start -Stack all -CreateMissing" -ForegroundColor Green
    exit 1
}

$stacksToProcess = Get-StacksToProcess $Stack

switch ($Action) {
    "start" {
        # Create network if needed
        if (-not (Create-Network)) {
            Write-Status "Cannot proceed without network" "Error"
            exit 1
        }
        
        # Start stacks in dependency order
        $sortedStacks = Get-SortedStacks $stacksToProcess
        Write-Header "Starting Stacks"
        
        foreach ($stackName in $sortedStacks) {
            Start-Stack $stackName
            Start-Sleep -Seconds 5
        }
        
        Write-Status "All requested stacks started" "Success"
    }
    
    "stop" {
        Write-Header "Stopping Stacks"
        
        # Stop in reverse dependency order
        $sortedStacks = Get-SortedStacks $stacksToProcess -ReverseOrder $true
        
        foreach ($stackName in $sortedStacks) {
            Stop-Stack $stackName
            Start-Sleep -Seconds 2
        }
        
        Write-Status "All requested stacks stopped" "Success"
    }
    
    "restart" {
        Write-Header "Restarting Stacks"
        
        $sortedStacks = Get-SortedStacks $stacksToProcess -ReverseOrder $true
        
        # Stop all
        foreach ($stackName in $sortedStacks) {
            Stop-Stack $stackName
        }
        
        Start-Sleep -Seconds 5
        
        # Start in correct order
        $sortedStacks = Get-SortedStacks $stacksToProcess
        foreach ($stackName in $sortedStacks) {
            Start-Stack $stackName
            Start-Sleep -Seconds 5
        }
        
        Write-Status "All requested stacks restarted" "Success"
    }
    
    "status" {
        Write-Header "Stack Status"
        
        foreach ($stackName in $stacksToProcess) {
            Get-StackStatus $stackName
        }
    }
    
    "health" {
        Write-Header "Stack Health Check"
        
        foreach ($stackName in $stacksToProcess) {
            Get-StackHealth $stackName
            Write-Host ""
        }
    }
    
    "logs" {
        foreach ($stackName in $stacksToProcess) {
            Show-StackLogs $stackName -Follow $Follow -Tail $Tail
        }
    }
    
    "validate" {
        Validate-Deployment
    }
    
    "cleanup" {
        Cleanup-Stack
    }
    
    default {
        Write-Status "Unknown action: $Action" "Error"
        exit 1
    }
}

Write-Host ""
Write-Status "Operation completed" "Success"
