@echo off
setlocal
set "ROOT=%~dp0.."
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUT=%ROOT%\build"

if not exist "%CSC%" (
  echo [ERROR] .NET Framework C# compiler not found: %CSC%
  exit /b 1
)
if not exist "%OUT%" mkdir "%OUT%"

"%CSC%" /nologo /target:exe /out:"%OUT%\RamFuryMonitor.exe" /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.ServiceProcess.dll "%ROOT%\src\RamFuryMonitor.cs" "%ROOT%\src\MonitorConfig.cs" "%ROOT%\src\RamFuryWindowsService.cs" || exit /b 1
"%CSC%" /nologo /target:winexe /out:"%OUT%\RamFuryTray.exe" /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%ROOT%\src\RamFuryTray.cs" || exit /b 1

echo [OK] Build completed: %OUT%
endlocal
