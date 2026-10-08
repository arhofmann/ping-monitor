@echo off
rem Builds PingMonitor.exe into this folder - as a single file that only needs the .NET runtime to start.
rem Requires the .NET SDK (https://dotnet.microsoft.com/download).
rem
rem For development and on Linux or macOS this script is not needed:
rem   dotnet run --project src/PingMonitor.Avalonia
setlocal
dotnet publish "%~dp0src\PingMonitor.Avalonia" --configuration Release --runtime win-x64 --output "%~dp0." ^
  -p:SelfContained=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded
