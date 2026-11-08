@echo off
pushd "%~dp0"
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy.ps1" %*
popd
