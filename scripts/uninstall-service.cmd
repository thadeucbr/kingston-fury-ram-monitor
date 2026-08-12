@echo off
setlocal
net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo [ERROR] Run this uninstaller as Administrator.
  exit /b 1
)

taskkill.exe /IM RamFuryTray.exe /F >nul 2>&1
sc.exe stop RamFuryMonitor >nul 2>&1
sc.exe delete RamFuryMonitor >nul 2>&1
reg.exe delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v RamFuryTray /f >nul 2>&1

echo [OK] RAM FURY Monitor service and startup entry removed.
echo Configuration and logs were kept in C:\ProgramData\RamFuryMonitor.
endlocal
