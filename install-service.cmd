@echo off
setlocal
set "SOURCE=%~dp0RamFuryMonitor.exe"
set "TRAY_SOURCE=%~dp0RamFuryTray.exe"
set "INSTALL_DIR=C:\ProgramData\RamFuryMonitor"
set "APP=%INSTALL_DIR%\RamFuryMonitor.exe"
set "TRAY=%INSTALL_DIR%\RamFuryTray.exe"

if not exist "%SOURCE%" (
  echo Executavel nao encontrado: %SOURCE%
  exit /b 1
)
if not exist "%TRAY_SOURCE%" (
  echo Tray nao encontrada: %TRAY_SOURCE%
  exit /b 1
)

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

rem Fecha a tray antiga para liberar o arquivo durante a atualizacao.
taskkill.exe /IM RamFuryTray.exe /F >nul 2>&1
sc.exe stop RamFuryMonitor >nul 2>&1
timeout /t 2 /nobreak >nul

copy /Y "%SOURCE%" "%APP%" >nul || (echo Falha ao copiar o servico.& exit /b 1)
copy /Y "%TRAY_SOURCE%" "%TRAY%" >nul || (echo Falha ao copiar a tray.& exit /b 1)

if not exist "%INSTALL_DIR%\config.json" echo {"enabled":true,"palette":"traffic","brightness":-1} > "%INSTALL_DIR%\config.json"

sc.exe delete RamFuryMonitor >nul 2>&1
sc.exe create RamFuryMonitor binPath= "\"%APP%\" --service" start= auto DisplayName= "RAM FURY Monitor"
sc.exe description RamFuryMonitor "Displays Windows RAM usage on Kingston FURY RGB memory LEDs."
sc.exe failure RamFuryMonitor reset= 86400 actions= restart/10000/restart/30000/restart/60000
reg.exe add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v RamFuryTray /t REG_SZ /d "\"%TRAY%\"" /f >nul
sc.exe start RamFuryMonitor
sc.exe query RamFuryMonitor
endlocal
