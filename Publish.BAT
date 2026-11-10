@echo off
setlocal EnableExtensions DisableDelayedExpansion

pushd "%~dp0" || (
    echo Could not open the project folder.
    pause
    exit /b 1
)

"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File ".\build.ps1" %*
set "publishExitCode=%ERRORLEVEL%"
popd

if not "%publishExitCode%"=="0" echo Publish failed with exit code %publishExitCode%.
pause
exit /b %publishExitCode%
