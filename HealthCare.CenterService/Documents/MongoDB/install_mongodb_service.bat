@echo off

:: BatchGotAdmin 
:------------------------------------- 
REM --> Check for permissions 
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system" 

REM --> If error flag set, we do not have admin. 
if '%errorlevel%' NEQ '0' ( 
echo Requesting administrative privileges... 
goto UACPrompt 
) else ( goto gotAdmin ) 

:UACPrompt 
echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs" 
echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs" 

"%temp%\getadmin.vbs" 
exit /B 

:gotAdmin 
if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" ) 
pushd "%CD%" 
CD /D "%~dp0" 
:--------------------------------------

if not exist "D:\MongoDB\Data" mkdir D:\MongoDB\Data
if not exist "D:\MongoDB\Log" mkdir D:\MongoDB\Log
if not exist "D:\MongoDB\Log\MongoDB.log" echo.>"D:\MongoDB\Log\MongoDB.Log"

@net stop MongoDB
@sc delete MongoDB
C:\"Program Files"\MongoDB\Server\3.6\bin\mongod.exe --dbpath D:\MongoDB\Data --directoryperdb --logpath D:\MongoDB\Log\MongoDB.log --logappend --install
net start MongoDB
