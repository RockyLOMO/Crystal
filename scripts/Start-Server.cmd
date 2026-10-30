@echo off
pwsh -NoLogo -NoProfile -File "%~dp0Manage-Mir2.ps1" -Action Start -RuntimeRoot "%~dp0." -ShowWindow
pause
