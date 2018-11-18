@echo off

%systemroot%/system32/inetsrv/appcmd.exe delete site "WebService"
%systemroot%/system32/inetsrv/appcmd.exe delete apppool "WebService"

netsh advfirewall firewall delete rule name="HealthCare.WebService"

pause