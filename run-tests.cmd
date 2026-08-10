@echo off
chcp 65001 >nul
cls

echo ========================================
echo  WUPM Test Runner
echo ========================================
echo.

dotnet test --verbosity normal

echo.
echo Tests completed.
pause
