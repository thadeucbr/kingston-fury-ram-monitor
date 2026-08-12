@echo off
setlocal EnableExtensions
set "ROOT=%~dp0.."
set "INSTALL_DIR=C:\ProgramData\RamFuryMonitor"
set "APP=%INSTALL_DIR%\RamFuryMonitor.exe"
set "TRAY=%INSTALL_DIR%\RamFuryTray.exe"
set "BUILD=%ROOT%\build"

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo [ERROR] Run this installer as Administrator.
  exit /b 1
)

call "%~dp0build.cmd" || exit /b 1
if not exist "%BUILD%\RamFuryMonitor.exe" exit /b 1
if not exist "%BUILD%\RamFuryTray.exe" exit /b 1

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
taskkill.exe /IM RamFuryTray.exe /F >nul 2>&1
sc.exe stop RamFuryMonitor >nul 2>&1
for /L %%I in (1,1,10) do (
  sc.exe query RamFuryMonitor 2>nul | findstr /I "STOPPED 1060" >nul && goto :stopped
  timeout /t 1 /nobreak >nul
)
:stopped
copy /Y "%BUILD%\RamFuryMonitor.exe" "%APP%" >nul || (echo [ERROR] Could not update service binary.& exit /b 1)
copy /Y "%BUILD%\RamFuryTray.exe" "%TRAY%" >nul || (echo [ERROR] Could not update tray binary.& exit /b 1)

if not exist "%INSTALL_DIR%\config.json" (
  >"%INSTALL_DIR%\config.json" echo {"enabled":true,"palette":"traffic","brightness":-1,"color":"#8C5000","savedPalettes":[]}
)

sc.exe query RamFuryMonitor >nul 2>&1
if "%errorlevel%"=="0" sc.exe delete RamFuryMonitor >nul 2>&1
sc.exe create RamFuryMonitor binPath= "\"%APP%\" --service" start= auto DisplayName= "RAM FURY Monitor" || exit /b 1
sc.exe description RamFuryMonitor "Displays Windows RAM usage on Kingston FURY RGB memory LEDs."
sc.exe failure RamFuryMonitor reset= 86400 actions= restart/10000/restart/30000/restart/60000
reg.exe add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v RamFuryTray /t REG_SZ /d "\"%TRAY%\"" /f >nul
sc.exe start RamFuryMonitor
sc.exe query RamFuryMonitor

echo [OK] RAM FURY Monitor installed.
endlocal
