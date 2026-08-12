@echo off
sc.exe stop RamFuryMonitor
sc.exe delete RamFuryMonitor
reg.exe delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v RamFuryTray /f
