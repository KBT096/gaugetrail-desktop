@echo off
setlocal
cd /d "%~dp0"
dotnet build GaugeTrail.sln -c Release --nologo
if errorlevel 1 exit /b %errorlevel%
dotnet run --project tests\GaugeTrail.SelfTest\GaugeTrail.SelfTest.csproj -c Release --no-build
exit /b %errorlevel%
