@echo off
pwsh -NoLogo -NoProfile -File "%~dp0Manage-Mir2.ps1" -Action Backup -RuntimeRoot "%~dp0."
pause
