@echo off

%systemroot%/system32/inetsrv/appcmd.exe stop apppool /apppool.name:"WebService"

net start W3SVC

%systemroot%/system32/inetsrv/appcmd.exe start apppool /apppool.name:"WebService"

pause