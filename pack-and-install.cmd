@echo off
REM Quick template pack and install for Windows
REM Double-click this file to build and install the template locally

echo.
echo ========================================
echo   Krafter Template - Pack and Install
echo ========================================
echo.

cd /d "%~dp0"

echo [1/4] Cleaning previous builds...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo [2/4] Packing template...
dotnet pack AditiKraft.Krafter.Templates.csproj -c Release
if errorlevel 1 (
    echo.
    echo ERROR: Pack failed!
    pause
    exit /b 1
)

echo [3/4] Uninstalling old version...
dotnet new uninstall AditiKraft.Krafter.Templates 2>nul

echo [4/4] Installing new version...
for %%f in (bin\Release\*.nupkg) do (
    dotnet new install "%%f"
    if errorlevel 1 (
        echo.
        echo ERROR: Installation failed!
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo   SUCCESS! Template installed.
echo ========================================
echo.
echo You can now create projects with:
echo   dotnet new krafter -n YourProjectName
echo.
echo To see the template:
echo   dotnet new list krafter
echo.
pause
