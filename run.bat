@echo off
setlocal
dotnet run --project "%~dp0src\MiniEnvi\MiniEnvi.csproj"
if errorlevel 1 pause
