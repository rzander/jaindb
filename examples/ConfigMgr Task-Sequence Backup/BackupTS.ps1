Import-Module (Join-Path $(Split-Path $env:SMS_ADMIN_UI_PATH) ConfigurationManager.psd1) 
$SiteCode = Get-PSDrive -PSProvider CMSITE
Push-Location "$($SiteCode.Name):\"

Get-CMTaskSequence | ForEach-Object { 
    $id = $_.PackageID
    Invoke-RestMethod -Uri "http://localhost:5000/uploadxml/$($id)" -Method Post -Body $_.Sequence -ContentType "application/json; charset=utf-8" 
}

