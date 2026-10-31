@echo off
pushd "%~dp0"
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-Server.ps1" %*
popd
pause
