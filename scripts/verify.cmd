@echo off
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1 -Configuration Release %*
