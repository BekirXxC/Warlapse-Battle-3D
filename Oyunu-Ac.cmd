@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Kur.ps1" -Launch
set "result=%ERRORLEVEL%"
if not "%result%"=="0" pause
exit /b %result%
