@echo off
setlocal
set "ROOT=%~dp0.."
set "DIST=%ROOT%\dist\RamFurySetup"
call "%~dp0build.cmd" || exit /b 1
if exist "%ROOT%\dist" rmdir /S /Q "%ROOT%\dist"
mkdir "%DIST%"
copy /Y "%ROOT%\build\RamFurySetup.exe" "%DIST%\RamFurySetup.exe" >nul
copy /Y "%ROOT%\build\RamFuryMonitor.exe" "%DIST%\RamFuryMonitor.exe" >nul
copy /Y "%ROOT%\build\RamFuryTray.exe" "%DIST%\RamFuryTray.exe" >nul
copy /Y "%ROOT%\build\ram-fury.ico" "%DIST%\ram-fury.ico" >nul
copy /Y "%ROOT%\build\ram-fury-installer-banner.png" "%DIST%\ram-fury-installer-banner.png" >nul
copy /Y "%ROOT%\README.md" "%DIST%\README.txt" >nul
powershell.exe -NoProfile -Command "Compress-Archive -Path '%DIST%\*' -DestinationPath '%ROOT%\dist\RamFurySetup.zip' -Force"
echo [OK] Package created: %ROOT%\dist
endlocal
