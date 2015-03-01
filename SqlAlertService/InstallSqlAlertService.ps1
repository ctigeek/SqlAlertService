$fullpath = (Get-Item -Path ".\" -Verbose).FullName
$cred = Get-Credential -Message "This service requires domain privledges."
New-Service -Name "SqlAlertService" -DisplayName "SqlAlertService" -Credential $cred -StartupType Automatic -BinaryPathName $fullpath\DataCollector.exe -Description "Runs SQL queries and sends an email if the results don't match the expected value."

