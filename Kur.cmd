@echo off
setlocal
title Warlapse Battle 3D 7.6.1 - Kurulum
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Kur.ps1" %*
set "result=%ERRORLEVEL%"
if not "%result%"=="0" echo Kurulum tamamlanamadi. Yukaridaki aciklamayi ve OKU.txt dosyasini kontrol edin.
echo.
pause
exit /b %result%
