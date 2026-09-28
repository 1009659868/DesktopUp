@echo off
setlocal
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  powershell.exe -NoLogo -NoProfile -File "%~dp0scripts\package-preview.ps1" %*
) else (
  pwsh.exe -NoLogo -NoProfile -File "%~dp0scripts\package-preview.ps1" %*
)
set "PreviewBuildExit=%ERRORLEVEL%"
if not "%PreviewBuildExit%"=="0" echo Preview build failed. Review the message above.
pause
exit /b %PreviewBuildExit%
