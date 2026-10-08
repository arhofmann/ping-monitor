@echo off
rem Builds and runs the tests of the core. PingMonitor.exe and the outage log are left untouched.
rem On Linux or macOS: dotnet run --project tests/PingMonitor.Core.Tests
dotnet run --project "%~dp0tests\PingMonitor.Core.Tests"
