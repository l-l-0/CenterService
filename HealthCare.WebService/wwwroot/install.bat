@echo off

%systemroot%/system32/inetsrv/appcmd.exe delete site "WebService"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "WebService"

%systemroot%/system32/inetsrv/appcmd.exe add apppool /name:WebService
%systemroot%/system32/inetsrv/appcmd.exe set apppool "WebService" /PipelineMode:"Integrated" /RuntimeVersion:"v4.0" /managedPipelineMode:"Integrated" /managedRuntimeVersion:"v4.0" /processModel.pingingEnabled:false /processModel.idleTimeout:"00:00:00" /recycling.periodicRestart.privateMemory:"0" /recycling.periodicRestart.time:"00:00:00"

%systemroot%/system32/inetsrv/appcmd.exe add site /name:WebService /bindings:"http/*:9090:" /physicalPath:"D:\WebService"
%systemroot%/system32/inetsrv/appcmd.exe set app "WebService/" /applicationpool:WebService


netsh advfirewall firewall delete rule name="HealthCare.WebService"
netsh advfirewall firewall add rule name="HealthCare.WebService" dir=in action=allow protocol=TCP localport=9090

pause