$ErrorActionPreference = "Stop"

$ProjectFolder = "BetterDiskCleanup.App"
$ProjectFile = "$ProjectFolder\BetterDiskCleanup.App.csproj"
$PublishDir = "Publish"
$Configuration = "Release"
$Runtime = "win-x64"

Write-Host "Building BetterDiskCleanup..." -ForegroundColor Cyan

# 1. Clean previous build
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

# 2. Publish as self-contained
Write-Host "Publishing as self-contained $Runtime..."
dotnet publish $ProjectFile -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Publish successful." -ForegroundColor Green

# 3. Create Inno Setup installer
Write-Host "Compiling Inno Setup Script..."
$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (Test-Path $InnoSetupCompiler) {
    & $InnoSetupCompiler "BetterDiskCleanup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Inno Setup compilation failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "Installer created successfully in Output folder." -ForegroundColor Green
} else {
    Write-Host "Inno Setup compiler not found. Skipping installer creation. Output is in $PublishDir" -ForegroundColor Yellow
}
