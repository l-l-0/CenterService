@ECHO OFF
if not exist "D:\mongodb\backup" mkdir "D:\mongodb\backup"
set "ymd=%date:~,4%%date:~5,2%%date:~8,2%"
"C:\Program Files\MongoDB\Server\3.4\bin\mongodump.exe" -o "D:\mongodb\backup\%ymd%"