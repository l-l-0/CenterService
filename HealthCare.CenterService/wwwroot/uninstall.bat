@echo off

%systemroot%/system32/inetsrv/appcmd.exe delete site "CenterService"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "CenterService"

%systemroot%/system32/inetsrv/appcmd.exe delete site "WebNg"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "WebNg"

%systemroot%/system32/inetsrv/appcmd.exe delete site "Simulate"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "Simulate"

netsh advfirewall firewall delete rule name="HealthCare.CenterService"

pause