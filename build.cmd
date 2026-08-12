@echo off
setlocal
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework C# compiler not found.
  exit /b 1
)
if not exist build mkdir build
"%CSC%" /nologo /target:exe /out:build\RamFuryMonitor.exe /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.ServiceProcess.dll RamFuryMonitor.cs || exit /b 1
"%CSC%" /nologo /target:winexe /out:build\RamFuryTray.exe /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll RamFuryTray.cs || exit /b 1
echo Build completed.
endlocal
