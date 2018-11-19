@echo off

%systemroot%/system32/inetsrv/appcmd.exe delete site "CenterService"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "CenterService"

%systemroot%/system32/inetsrv/appcmd.exe add apppool /name:CenterService
%systemroot%/system32/inetsrv/appcmd.exe set apppool "CenterService" /PipelineMode:"Integrated" /RuntimeVersion:"v4.0" /managedPipelineMode:"Integrated" /managedRuntimeVersion:"v4.0" /processModel.pingingEnabled:false /processModel.idleTimeout:"00:00:00" /recycling.periodicRestart.privateMemory:"0" /recycling.periodicRestart.time:"00:00:00"

%systemroot%/system32/inetsrv/appcmd.exe add site /name:CenterService /bindings:"http/*:8000:" /physicalPath:"D:\CenterService"
%systemroot%/system32/inetsrv/appcmd.exe set app "CenterService/" /applicationpool:CenterService


%systemroot%/system32/inetsrv/appcmd.exe delete site "WebNg"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "WebNg"

%systemroot%/system32/inetsrv/appcmd.exe add apppool /name:WebNg
%systemroot%/system32/inetsrv/appcmd.exe set apppool "WebNg" /PipelineMode:"Integrated" /RuntimeVersion:"v4.0" /managedPipelineMode:"Integrated" /managedRuntimeVersion:"v4.0" /processModel.pingingEnabled:false

%systemroot%/system32/inetsrv/appcmd.exe add site /name:WebNg /bindings:"http/*:8080:" /physicalPath:"D:\WebNg"
%systemroot%/system32/inetsrv/appcmd.exe set app "WebNg/" /applicationpool:WebNg


%systemroot%/system32/inetsrv/appcmd.exe delete site "Simulate"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "Simulate"

%systemroot%/system32/inetsrv/appcmd.exe add apppool /name:Simulate
%systemroot%/system32/inetsrv/appcmd.exe set apppool "Simulate" /PipelineMode:"Integrated" /RuntimeVersion:"v4.0" /managedPipelineMode:"Integrated" /managedRuntimeVersion:"v4.0" /processModel.pingingEnabled:false

%systemroot%/system32/inetsrv/appcmd.exe add site /name:Simulate /bindings:"http/*:8160:" /physicalPath:"D:\Simulate"
%systemroot%/system32/inetsrv/appcmd.exe set app "Simulate/" /applicationpool:Simulate

REM 删除默认的站点和域
%systemroot%/system32/inetsrv/appcmd.exe delete site "Default Web Site"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool ".NET v4.5"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool ".NET v4.5 Classic"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "DefaultAppPool"

netsh advfirewall firewall delete rule name="HealthCare.CenterService"
netsh advfirewall firewall add rule name="HealthCare.CenterService" dir=in action=allow protocol=TCP localport=27017,80,8000,8080,9090

pause