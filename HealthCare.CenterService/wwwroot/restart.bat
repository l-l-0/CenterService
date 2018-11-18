@echo off

%systemroot%/system32/inetsrv/appcmd.exe stop apppool /apppool.name:"CenterService"
%systemroot%/system32/inetsrv/appcmd.exe stop apppool /apppool.name:"WebNg"
%systemroot%/system32/inetsrv/appcmd.exe stop apppool /apppool.name:"Simulate"


net start W3SVC

%systemroot%/system32/inetsrv/appcmd.exe start apppool /apppool.name:"CenterService"
%systemroot%/system32/inetsrv/appcmd.exe start apppool /apppool.name:"WebNg"
%systemroot%/system32/inetsrv/appcmd.exe stop apppool /apppool.name:"Simulate"


pause