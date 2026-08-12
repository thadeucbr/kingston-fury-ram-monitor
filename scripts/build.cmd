@echo off
setlocal
set "ROOT=%~dp0.."
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUT=%ROOT%\build"
for %%I in ("%ROOT%\assets\branding\ram-fury.ico") do set "ICON=%%~fI"

if not exist "%CSC%" (
  echo [ERROR] .NET Framework C# compiler not found: %CSC%
  exit /b 1
)
if not exist "%ICON%" (
  echo [ERROR] Branding icon not found: %ICON%
  exit /b 1
)
if not exist "%OUT%" mkdir "%OUT%"
copy /Y "%ICON%" "%OUT%\ram-fury.ico" >nul || (echo [ERROR] Could not copy icon.& exit /b 1)
copy /Y "%ROOT%\assets\branding\ram-fury-installer-banner.png" "%OUT%\ram-fury-installer-banner.png" >nul || (echo [ERROR] Could not copy banner.& exit /b 1)

"%CSC%" /nologo /target:exe /win32icon:"%ICON%" /out:"%OUT%\RamFuryMonitor.exe" /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.ServiceProcess.dll "%ROOT%\src\RamFuryMonitor.cs" "%ROOT%\src\MonitorConfig.cs" "%ROOT%\src\RamFuryWindowsService.cs" || exit /b 1
"%CSC%" /nologo /target:winexe /win32icon:"%ICON%" /out:"%OUT%\RamFuryTray.exe" /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%ROOT%\src\RamFuryTray.cs" || exit /b 1
"%CSC%" /nologo /target:winexe /win32manifest:"%ROOT%\assets\RamFurySetup.manifest" /win32icon:"%ICON%" /out:"%OUT%\RamFurySetup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%ROOT%\src\RamFurySetup.cs" || exit /b 1

echo [OK] Build completed: %OUT%
endlocal
