Stop-Service "SqlAlertService"
Start-Sleep -Seconds 3

$service = Get-WmiObject -Class Win32_Service -Filter "Name='SqlAlertService'"
$service.delete()
