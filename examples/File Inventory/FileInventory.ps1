function GetMD5([string]$txt) {
    $md5 = new-object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider
    $utf8 = new-object -TypeName System.Text.ASCIIEncoding
    return Base58(@(0xd5, 0x10) + $md5.ComputeHash($utf8.GetBytes($txt))) #To store hash in Miltihash format, we add a 0xD5 to make it an MD5 and an 0x10 means 10Bytes length
}

function Base58([byte[]]$data) {
    $Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"
    [bigint]$intData = 0
    for ($i = 0; $i -lt $data.Length; $i++) {
        $intData = ($intData * 256) + $data[$i]; 
    }
    [string]$result = "";
    while ($intData -gt 0) {
        $remainder = ($intData % 58);
        $intData /= 58;
        $result = $Digits[$remainder] + $result;
    }

    for ($i = 0; ($i -lt $data.Length) -and ($data[$i] -eq 0); $i++) {
        $result = '1' + $result;
    }

    return $result
}

Get-ChildItem -file -Recurse -ea SilentlyContinue| ForEach-Object { 
    $object = New-Object PSObject
    $object | Add-Member -MemberType NoteProperty -Name "Name" -Value $_.Name
    $object | Add-Member -MemberType NoteProperty -Name "#FullName" -Value $_.FullName
    $object | Add-Member -MemberType NoteProperty -Name "Attributes" -Value $_.Attributes
    $object | Add-Member -MemberType NoteProperty -Name "@CreationTimeUtc" -Value $_.CreationTimeUtc
    $object | Add-Member -MemberType NoteProperty -Name "DirectoryName" -Value $_.DirectoryName
    $object | Add-Member -MemberType NoteProperty -Name "Extension" -Value $_.Extension
    $object | Add-Member -MemberType NoteProperty -Name "IsReadOnly" -Value $_.IsReadOnly
    $object | Add-Member -MemberType NoteProperty -Name "@LastWriteTimeUtc" -Value $_.LastWriteTimeUtc
    $object | Add-Member -MemberType NoteProperty -Name "Length" -Value $_.Length
    $object | Add-Member -MemberType NoteProperty -Name "VersionInfo" -Value ($_.VersionInfo | Select-Object -Property * -ExcludeProperty FileName)
    #$object | Add-Member -MemberType NoteProperty -Name "#FileHash" -Value (Get-FileHash $_.FullName -Algorithm MD5).Hash 
    #$id = GetMD5($object.'#FullName' + $object.'#FileHash')
    $id = GetMD5($object.'#FullName')
    if ($id) {
        $con = $($object| ConvertTo-Json -Compress); 
        Invoke-RestMethod -Uri "http://192.168.2.146:5000/upload/$($id)" -Method Post -Body $con -ContentType "application/json; charset=utf-8" 
    }
}