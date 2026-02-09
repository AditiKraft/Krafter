# PowerShell script to publish the Krafter template to NuGet
# Usage: .\scripts\publish-template.ps1 -ApiKey "your-api-key" [-Source "nuget-source"]

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,
    
    [string]$Source = "https://api.nuget.org/v3/index.json",
    
    [string]$Configuration = "Release",
    
    [switch]$SkipBuild
)

Write-Host "🚀 Publishing Krafter Template to NuGet..." -ForegroundColor Cyan

# Navigate to root directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Split-Path -Parent $scriptPath
Set-Location $rootPath

# Build if not skipped
if (-not $SkipBuild) {
    Write-Host "🔨 Building template package..." -ForegroundColor Yellow
    
    # Clean previous builds
    if (Test-Path "bin") {
        Remove-Item -Recurse -Force "bin"
    }
    if (Test-Path "obj") {
        Remove-Item -Recurse -Force "obj"
    }
    
    # Pack the template
    dotnet pack AditiKraft.Krafter.Templates.csproj -c $Configuration
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Find the package
$packagePath = Get-ChildItem -Path "bin\$Configuration" -Filter "*.nupkg" | Select-Object -First 1

if ($null -eq $packagePath) {
    Write-Host "❌ Package not found! Run with -SkipBuild `$false to build first." -ForegroundColor Red
    exit 1
}

Write-Host "📦 Package found: $($packagePath.Name)" -ForegroundColor Green

# Confirm before publishing
Write-Host "`n⚠️  You are about to publish to: $Source" -ForegroundColor Yellow
Write-Host "Package: $($packagePath.Name)" -ForegroundColor Yellow
$confirmation = Read-Host "Continue? (y/n)"

if ($confirmation -ne 'y') {
    Write-Host "❌ Publishing cancelled." -ForegroundColor Red
    exit 0
}

# Push to NuGet
Write-Host "`n📤 Pushing to NuGet..." -ForegroundColor Cyan
dotnet nuget push $packagePath.FullName --source $Source --api-key $ApiKey

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Successfully published to NuGet!" -ForegroundColor Green
    Write-Host "🌐 Package will be available at: https://www.nuget.org/packages/Krafter.Templates" -ForegroundColor Cyan
    Write-Host "⏱️  Note: It may take 5-10 minutes for the package to be indexed and searchable." -ForegroundColor Gray
    
    Write-Host "`n📋 Users can install with:" -ForegroundColor Cyan
    Write-Host "   dotnet new install Krafter.Templates" -ForegroundColor White
} else {
    Write-Host "`n❌ Publishing failed!" -ForegroundColor Red
    Write-Host "Check the error message above for details." -ForegroundColor Yellow
    exit 1
}
