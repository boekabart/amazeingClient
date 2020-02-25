@echo off
dotnet build -c Release >nul
if ERRORLEVEL 1 echo Build failed
if ERRORLEVEL 1 goto :eind

if not defined AMAZEING_KEY echo please set AMAZEING_KEY
if not defined AMAZEING_NAME echo please set AMAZEING_NAME
if not defined AMAZEING_KEY goto :eind
if not defined AMAZEING_NAME goto :eind

dotnet run -c Release --no-build -- "%AMAZEING_KEY%" "%AMAZEING_NAME%" http://localhost:5500 %1 %2 %3 %4 %5 %6 %7 %8 %9
:eind
