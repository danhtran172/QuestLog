@echo off
setlocal

cd /d "%~dp0"
dotnet run --project "MasterPlay.UI\MasterPlay.UI.csproj"
