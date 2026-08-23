@echo off
setlocal
cd /d "%~dp0"
dotnet run --project src\GaugeTrail.Desktop\GaugeTrail.Desktop.csproj -c Release
exit /b %errorlevel%
