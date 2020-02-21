@echo off

dotnet build -c Release >nul
if ERRORLEVEL 1 echo Build failed
if ERRORLEVEL 1 goto :eind

if not defined AMAZEING_KEY echo please set AMAZEING_KEY
if not defined AMAZEING_NAME echo please set AMAZEING_NAME
if not defined AMAZEING_KEY goto :eind
if not defined AMAZEING_NAME goto :eind


set MyDate=
for /f "skip=1" %%x in ('wmic os get localdatetime') do if not defined MyDate set MyDate=%%x

set datetimef=%MyDate:~0,8%_%MyDate:~8,6%

FOR /F "usebackq delims=" %%a IN (`git rev-parse HEAD`) DO (
  SET COMMIT=%%~a
  SHIFT
)

set FN=.output\%datetimef%_%COMMIT%.txt

if not exist .output mkdir .output >nul
dotnet run --no-build -c Release "%AMAZEING_KEY%" "%AMAZEING_NAME%" https://maze.hightechict.nl >%FN%
echo ----------------------------------------- >>%FN%
echo -----------------------------------------
type %FN%
git log -1 >>%FN%
echo ----------------------------------------- >>%FN%
git status -s >>%FN%
echo ----------------------------------------- >>%FN%
git diff >>%FN%
echo ----------------------------------------- >>%FN%

:eind