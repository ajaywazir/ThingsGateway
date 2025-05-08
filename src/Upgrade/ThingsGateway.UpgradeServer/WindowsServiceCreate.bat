cd ..
sc create ThingsGateway binPath= %~dp0ThingsGateway.UpgradeServer.exe start= auto 
sc description ThingsGateway.UpgradeServer "ThingsGateway.UpgradeServer"
Net Start ThingsGateway.UpgradeServer
pause
