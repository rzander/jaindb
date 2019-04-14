function GetHash([string]$txt) {
    return GetMD5($txt)
}

function GetMD5([string]$txt) {
    $md5 = new-object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider
    $utf8 = new-object -TypeName System.Text.ASCIIEncoding
    return Base58(@(0xd5, 0x10) + $md5.ComputeHash($utf8.GetBytes($txt))) #To store hash in Miltihash format, we add a 0xD5 to make it an MD5 and an 0x10 means 10Bytes length
}

function GetSHA2_256([string]$txt) {
    $sha = new-object -TypeName System.Security.Cryptography.SHA256CryptoServiceProvider
    $utf8 = new-object -TypeName System.Text.ASCIIEncoding
    return Base58(@(0x12, 0x20) + $sha.ComputeHash($utf8.GetBytes($txt))) #To store hash in Miltihash format, we add a 0x12 to make it an SHA256 and an 0x20 means 32Bytes length
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

function normalize([long]$number) {
    if ($number) {
        if ($number -gt 2000000000 ) { return ([math]::Truncate($number / 1000000000) * 1000000000) }
        if ($number -gt 100000000 ) { return ([math]::Truncate($number / 1000000) * 1000000) }
        if ($number -gt 1000000 ) { return ([math]::Truncate($number / 10000) * 10000) }
    }
    return $number
}

function GetInv {
    Param(
        [parameter(Mandatory = $true)]
        [String]
        $Name,
        [String]
        $Namespace,
        [parameter(Mandatory = $true)]
        [String]
        $WMIClass,
        [String[]]
        $Properties,
        [ref]
        $AppendObject,
        $AppendProperties
    )

    if($Namespace) {} else { $Namespace = "root\cimv2"}
    $obj = Get-CimInstance -Namespace $Namespace -ClassName $WMIClass

    if ($Properties -eq $null) { $Properties = $obj.Properties.Name | Sort-Object }
    if ($Namespace -eq $null) { $Namespace = "root\cimv2"}

    $res = $obj | Select-Object $Properties -ea SilentlyContinue

    #WMI Results can be an array of objects
    if ($obj -is [array]) {
        $Properties  | ForEach-Object { $prop = $_; $i = 0; $res | ForEach-Object {
                $val = $obj[$i].($prop.TrimStart('#@'));
                try {
                    if ($val.GetType() -eq [string]) {
                        $val = $val.Trim();
                        if (($val.Length -eq 25) -and ($val.IndexOf('.') -eq 14) -and ($val.IndexOf('+') -eq 21)) {
                            $OS = Get-WmiObject -class Win32_OperatingSystem
                            $val = $OS.ConvertToDateTime($val)
                        }
                    }
                }
                catch {}
                if ($val) {
                    $_ | Add-Member -MemberType NoteProperty -Name ($prop) -Value ($val) -Force;
                }
                else {
                    $_.PSObject.Properties.Remove($prop);
                }
                $i++
            }
        } 
    }
    else {
        $Properties | ForEach-Object { 
            $prop = $_;
            $val = $obj.($prop.TrimStart('#@'));
            try {
                if ($val.GetType() -eq [string]) {
                    $val = $val.Trim();
                    if (($val.Length -eq 25) -and ($val.IndexOf('.') -eq 14) -and ($val.IndexOf('+') -eq 21)) {
                        $OS = Get-WmiObject -class Win32_OperatingSystem
                        $val = $OS.ConvertToDateTime($val)
                    }
                }
            }
            catch {}
            if ($val) {
                $res |  Add-Member -MemberType NoteProperty -Name ($prop) -Value ($val) -Force;
            }
            else {
                $res.PSObject.Properties.Remove($prop);
            }
        }
            
    }
        
    
    $res.psobject.TypeNames.Insert(0, $Name) 

    if ($AppendProperties -ne $null) {
        $AppendProperties.PSObject.Properties | ForEach-Object {
            if ($_.Value) {
                $res | Add-Member -MemberType NoteProperty -Name $_.Name -Value ($_.Value)
            }
        } 
    }

    if ($AppendObject.Value -ne $null) {
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name $Name -Value ($res)
        return $null
    }

    return $res
    
}

function GetMyID {
    $uuid = getinv -Name "Computer" -WMIClass "win32_ComputerSystemProduct" -Properties @("#UUID")
    $comp = getinv -Name "Computer" -WMIClass "win32_ComputerSystem" -Properties @("Domain", "#Name") -AppendProperties $uuid 
    return GetHash($comp | ConvertTo-Json -Compress)
}

function SetID {
    Param(
        [ref]
        $AppendObject )

    if ($AppendObject.Value -ne $null) {
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "#id" -Value (GetMyID) -ea SilentlyContinue
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "#UUID" -Value (getinv -Name "Computer" -WMIClass "win32_ComputerSystemProduct" -Properties @("#UUID"))."#UUID" -ea SilentlyContinue
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "#Name" -Value (getinv -Name "Computer" -WMIClass "win32_ComputerSystem" -Properties @("Name"))."Name" -ea SilentlyContinue
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "#SerialNumber" -Value (getinv -Name "Computer" -WMIClass "win32_SystemEnclosure" -Properties @("SerialNumber"))."SerialNumber" -ea SilentlyContinue
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "@MAC" -Value (get-wmiobject -class "Win32_NetworkAdapterConfiguration" | Where-Object {($_.IpEnabled -Match "True")}).MACAddress.Replace(':', '-')
		
		[xml]$xml = Get-Content "$($env:programfiles)\DevCDRAgent\DevCDRAgent.exe.config"
		$inst = $xml.configuration.applicationSettings.'DevCDRAgent.Properties.Settings'.setting | Where-Object { $_.name -eq 'Instance' }
		$AppendObject.Value | Add-Member -MemberType NoteProperty -Name "DevCDRInstance" -Value $inst.value
        return $null
    }   
}


$object = New-Object PSObject
getinv -Name "Battery" -WMIClass "win32_Battery" -Properties @("@BatteryStatus", "Caption", "Chemistry", "#Name", "@Status", "PowerManagementCapabilities", "#DeviceID") -AppendObject ([ref]$object)
$bios = getinv -Name "BIOS" -WMIClass "win32_BIOS" -Properties @("Name", "Manufacturer", "Version", "#SerialNumber") #-AppendObject ([ref]$object)
$bios | Add-Member -MemberType NoteProperty -Name "@DeviceHardwareData" -Value ((Get-WMIObject -Namespace root/cimv2/mdm/dmmap -Class MDM_DevDetail_Ext01 -Filter "InstanceID='Ext' AND ParentID='./DevDetail'").DeviceHardwareData) -ea SilentlyContinue
$object | Add-Member -MemberType NoteProperty -Name "BIOS" -Value $bios -ea SilentlyContinue
getinv -Name "Processor" -WMIClass "win32_Processor" -Properties @("Name", "Manufacturer", "Family", "NumberOfCores", "NumberOfEnabledCore", "NumberOfLogicalProcessors", "L2CacheSize", "L3CacheSize", "#ProcessorId") -AppendObject ([ref]$object)
getinv -Name "Memory" -WMIClass "win32_PhysicalMemory" -Properties @("Manufacturer", "ConfiguredClockSpeed", "ConfiguredVoltage", "PartNumber", "FormFactor", "DataWidth", "Speed", "SMBIOSMemoryType", "Name" , "Capacity" , "#SerialNumber") -AppendObject ([ref]$object)
getinv -Name "OS" -WMIClass "win32_OperatingSystem" -Properties @("BuildNumber", "BootDevice", "Caption", "CodeSet", "CountryCode", "@CurrentTimeZone", "EncryptionLevel", "Locale", "Manufacturer", "MUILanguages", "OperatingSystemSKU", "OSArchitecture", "OSLanguage", "SystemDrive", "Version", "#InstallDate", "@LastBootUpTime") -AppendObject ([ref]$object)

$CSP = getinv -Name "Computer" -WMIClass "win32_ComputerSystemProduct" -Properties @("#UUID", "Version")
$CS = getinv -Name "Computer" -WMIClass "win32_ComputerSystem" -Properties @("Domain", "HypervisorPresent", "InfraredSupported", "Manufacturer", "Model", "PartOfDomain", "@Roles", "SystemFamily", "SystemSKUNumber", "@UserName", "WakeUpType", "TotalPhysicalMemory", "#Name") -AppendProperties $CSP 
getinv -Name "Computer" -WMIClass "win32_SystemEnclosure" -Properties @("ChassisTypes", "Model", "#SMBIOSAssetTag", "#SerialNumber") -AppendProperties $CS -AppendObject ([ref]$object)

getinv -Name "DiskDrive" -WMIClass "Win32_DiskDrive" -Properties @("@Capabilities", "Caption", "DeviceID", "@FirmwareRevision", "@Index", "InterfaceType", "MediaType", "Model", "@Partitions", "PNPDeviceID", "Size", "#SerialNumber" ) -AppendObject ([ref]$object)
getinv -Name "DiskPartition" -WMIClass "Win32_DiskPartition" -Properties @("BlockSize", "Bootable", "BootPartition", "DeviceID", "DiskIndex", "Index", "Size", "Type") -AppendObject ([ref]$object)

$ld = getinv -Name "LogicalDisk" -WMIClass "Win32_LogicalDisk" -Properties @("DeviceID", "DriveType", "FileSystem", "MediaType", "Size", "VolumeName", "@FreeSpace", "#VolumeSerialNumber") # -AppendObject ([ref]$object)
$ld = $ld | Where-Object { $_.DriveType -lt 4 }
$object | Add-Member -MemberType NoteProperty -Name "LogicalDisk" -Value ($ld)

#getinv -Name "NetworkAdapter" -WMIClass "Win32_NetworkAdapter" -Properties @("AdapterType", "Description", "@DeviceID", "@InterfaceIndex", "#MACAddress", "Manufacturer", "PhysicalAdapter", "PNPDeviceID", "ServiceName", "@Speed") -AppendObject ([ref]$object)
#getinv -Name "NetworkAdapterConfiguration" -WMIClass "Win32_NetworkAdapterConfiguration" -Properties @("@DefaultIPGateway", "Description", "DHCPEnabled", "#DHCPServer", "DNSDomain", "@Index", "@InterfaceIndex", "@IPAddress", "IPEnabled", "@IPSubnet", "#MACAddress" ) -AppendObject ([ref]$object)

getinv -Name "Video" -WMIClass "Win32_VideoController" -Properties @("AdapterCompatibility", "Name", "@CurrentHorizontalResolution", "@CurrentVerticalResolution", "@CurrentBitsPerPixel", "@CurrentRefreshRate", "DriverVersion", "InfSection", "PNPDeviceID", "VideoArchitecture", "VideoProcessor") -AppendObject ([ref]$object)
$object.Video= $object.Video | where { $_.PNPDeviceID -notlike "USB*" } #Cleanup USB Video Devices
getinv -Name "QFE" -WMIClass "Win32_QuickFixEngineering" -Properties @("Caption", "Description", "HotFixID", "@InstalledOn") -AppendObject ([ref]$object)
getinv -Name "Share" -WMIClass "Win32_Share" -Properties @("Name", "Description ", "Name", "Path", "Type") -AppendObject ([ref]$object)
getinv -Name "Audio" -WMIClass "Win32_SoundDevice" -Properties @("Caption", "Description ", "DeviceID", "Manufacturer") -AppendObject ([ref]$object)
$object.Audio = $object.Audio | where { $_.DeviceID -notlike "USB*" } #Cleanup USB Audio Devices

getinv -Name "CDROM" -WMIClass "Win32_CDROMDrive" -Properties @("Capabilities", "Name", "Description", "DeviceID", "@Drive" , "MediaType") -AppendObject ([ref]$object)

#getinv -Name "Driver" -WMIClass "Win32_PnPSignedDriver" -Properties @("DeviceID","DeviceName", "DriverDate", "DriverProviderName", "DriverVersion", "FriendlyName", "HardWareID", "InfName" ) -AppendObject ([ref]$object)
getinv -Name "Printer" -WMIClass "Win32_Printer" -Properties @("DeviceID","CapabilityDescriptions","DriverName", "Local" , "Network", "PrinterPaperNames") -AppendObject ([ref]$object)

#getinv -Name "OptionalFeature" -WMIClass "Win32_OptionalFeature" -Properties @("Caption", "Name", "InstallState" ) -AppendObject ([ref]$object)
$feature = Get-WindowsOptionalFeature -Online | Select-Object @{N = 'Name'; E = {$_.FeatureName}} , @{N = 'InstallState'; E = {$_.State.tostring()}} 
$object | Add-Member -MemberType NoteProperty -Name "OptionalFeature" -Value ($feature)

$user = Get-LocalUser | Select-Object Description, Enabled, UserMayChangePassword, PasswordRequired, Name, @{N = '@PasswordLastSet'; E = {[System.DateTime](($_.PasswordLastSet).ToUniversalTime())}}, @{N = 'id'; E = {$_.SID.Value.ToString()}} | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalUsers" -Value ($user)

$locAdmin = Get-LocalGroupMember -SID S-1-5-32-544 | Select-Object @{N = 'Name'; E = {$_.Name.Replace($($env:Computername) + "\", "")}}, ObjectClass, @{Name = 'PrincipalSource'; Expression = {$_.PrincipalSource.ToString()}}, @{Name = 'id'; Expression = {$_.SID.Value.ToString()}} | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalAdmins" -Value ($locAdmin)

$locGroup = Get-LocalGroup | Select-Object Description, Name, PrincipalSource, ObjectClass, @{N = 'id'; E = {$_.SID.Value.ToString()}}  | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalGroups" -Value ($locGroup)

$fw = Get-NetFirewallProfile | select Name, Enabled
$object | Add-Member -MemberType NoteProperty -Name "Firewall" -Value ($fw)

$tpm = get-tpm
$object | Add-Member -MemberType NoteProperty -Name "TPM" -Value ($tpm)

$bitlocker = Get-BitLockerVolume | ? { $_.VolumeType -eq 'OperatingSystem' } | select MountPoint, @{N = 'EncryptionMethod'; E = {$_.EncryptionMethod.ToString()}} , AutoUnlockEnabled, AutoUnlockKeyStored, MetadataVersion, VolumeStatus, ProtectionStatus, LockStatus, EncryptionPercentage, WipePercentage, @{N = 'VolumeType'; E = {$_.VolumeType.ToString()}}, KeyProtector | ConvertTo-Json | ConvertFrom-Json
$bitlocker.KeyProtector | % { $_ | Add-Member -MemberType NoteProperty -Name "#RecoveryPassword" -Value ($_.RecoveryPassword)}
$bitlocker.KeyProtector | % { $_.PSObject.Properties.Remove('KeyProtectorId'); $_.PSObject.Properties.Remove('RecoveryPassword') }
$object | Add-Member -MemberType NoteProperty -Name "BitLocker" -Value ($bitlocker)

$defender = Get-MpPreference | select * -ExcludeProperty ComputerID, PSComputerName, Cim*
$object | Add-Member -MemberType NoteProperty -Name "Defender" -Value ($defender)

#$FWRules = Get-NetFirewallRule | Select-Object DisplayName,Description,DisplayGroup,Group,Enabled,Profile,Platform,Direction,Action,EdgeTraversalPolicy,LooseSourceMapping,LocalOnlyMapping,Owner,PrimaryStatus,Status,EnforcementStatus,PolicyStoreSource,PolicyStoreSourceType | Sort-Object -Property DisplayName
#$object | Add-Member -MemberType NoteProperty -Name "FirewallRules" -Value ($FWRules)

#Windows Universal Apps
#$Appx = Get-AppxPackage | Select-Object Name, Publisher, Architecture, Version, PackageFullName, IsFramework, PackageFamilyName, PublisherId, IsResourcePackage, IsBundle, IsDevelopmentMode, IsPartiallyStaged, SignatureKind, Status | Sort-Object -Property Name
#$object | Add-Member -MemberType NoteProperty -Name "AppX" -Value ($Appx | Sort-Object -Property Name )

#Windows Updates
#$objSearcher = (New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher();
#$objResults = $objSearcher.Search('IsHidden=0');
#$upd += $objResults.Updates | Select-Object -Property @{n='@IsInstalled';e={$_.IsInstalled}},@{n='KB';e={$_.KBArticleIDs}},@{n='Bulletin';e={$_.SecurityBulletinIDs.Item(0)}},@{n='Title';e={$_.Title}},@{n='UpdateID';e={$_.Identity.UpdateID}},@{n='Revision';e={$_.Identity.RevisionNumber}},@{n='LastChange';e={$_.LastDeploymentChangeTime}}
#$object | Add-Member -MemberType NoteProperty -Name "Update" -Value ($upd)

#Get Installed Software
$SW = Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* -ea SilentlyContinue | Where-Object { $_.DisplayName -ne $null -and $_.SystemComponent -ne 0x1 -and $_.ParentDisplayName -eq $null } | Select-Object DisplayName, DisplayVersion, Publisher, Language, WindowsInstaller, @{N = '@InstallDate'; E = { $_.InstallDate }}, HelpLink, UninstallString, @{N = 'Architecture'; E = {"X64"}}, @{N = 'id'; E = {GetHash($_.DisplayName + $_.DisplayVersion + $_.Publisher + "X64")}}
$SW += Get-ItemProperty HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ea SilentlyContinue | Where-Object { $_.DisplayName -ne $null -and $_.SystemComponent -ne 0x1 -and $_.ParentDisplayName -eq $null } | Select-Object DisplayName, DisplayVersion, Publisher, Language, WindowsInstaller,  @{N = '@InstallDate'; E = { $_.InstallDate }}, HelpLink, UninstallString,  @{N = 'Architecture'; E = {"X86"}}, @{N = 'id'; E = {GetHash($_.DisplayName + $_.DisplayVersion + $_.Publisher + "X86")}}
$object | Add-Member -MemberType NoteProperty -Name "Software" -Value ($SW| Sort-Object -Property DisplayName )

#Services ( Exlude services with repeating numbers like BluetoothUserService_62541)
$Services = get-service | Where-Object { (($_.Name.IndexOf("_")) -eq -1) -and ($_.Name -ne 'camsvc')  } | Select-Object -Property @{N = 'id'; E = { $_.Name}}, DisplayName, @{N = 'StartType'; E = {if($_.StartType -eq 4) { 'Disabled'} else { 'Manual or Automatic' }}}, @{N = '@status'; E = {$_.status}} 
$object | Add-Member -MemberType NoteProperty -Name "Services" -Value ($Services )

#Office365
$O365 = Get-ItemProperty HKLM:SOFTWARE\Microsoft\Office\ClickToRun\Configuration -ea SilentlyContinue  | Select * -Exclude PS*,*Retail.EmailAddress,InstallID
$object | Add-Member -MemberType NoteProperty -Name "Office365" -Value ($O365)

#CloudJoin
$Cloud = Get-Item HKLM:SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo\* -ea SilentlyContinue  | Get-ItemProperty | select -Property IdpDomain,TenantId,@{N = '#UserEmail'; E = { $_.UserEmail}},AikCertStatus, AttestationLevel,TransportKeyStatus
$object | Add-Member -MemberType NoteProperty -Name "CloudJoin" -Value ($Cloud)

#OS Version details
$UBR = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name UBR).UBR

#Cleanup
$object."LogicalDisk" | ForEach-Object { $_."@FreeSpace" = normalize($_."@FreeSpace")}
$object.Computer."TotalPhysicalMemory" = normalize($object.Computer."TotalPhysicalMemory")
$object.OS.Version = $object.OS.Version + "." + $UBR


SetID([ref] $object)

$id = $object."#id"
$con = $object | ConvertTo-Json -Depth 5 -Compress
Write-Host "Device ID: $($id)"
Write-Host "Hash:" (Invoke-RestMethod -Uri "%LocalURL%:%WebPort%/upload/$($id)" -Method Post -Body $con -ContentType "application/json; charset=utf-8")

# SIG # Begin signature block
# MIIOEgYJKoZIhvcNAQcCoIIOAzCCDf8CAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUfvUBo1f9epiShLllzM2E7zQN
# ihKgggtIMIIFYDCCBEigAwIBAgIRANsn6eS1hYK93tsNS/iNfzcwDQYJKoZIhvcN
# AQELBQAwfTELMAkGA1UEBhMCR0IxGzAZBgNVBAgTEkdyZWF0ZXIgTWFuY2hlc3Rl
# cjEQMA4GA1UEBxMHU2FsZm9yZDEaMBgGA1UEChMRQ09NT0RPIENBIExpbWl0ZWQx
# IzAhBgNVBAMTGkNPTU9ETyBSU0EgQ29kZSBTaWduaW5nIENBMB4XDTE4MDUyMjAw
# MDAwMFoXDTIxMDUyMTIzNTk1OVowgawxCzAJBgNVBAYTAkNIMQ0wCwYDVQQRDAQ4
# NDgzMQswCQYDVQQIDAJaSDESMBAGA1UEBwwJS29sbGJydW5uMRkwFwYDVQQJDBBI
# YWxkZW5zdHJhc3NlIDMxMQ0wCwYDVQQSDAQ4NDgzMRUwEwYDVQQKDAxSb2dlciBa
# YW5kZXIxFTATBgNVBAsMDFphbmRlciBUb29sczEVMBMGA1UEAwwMUm9nZXIgWmFu
# ZGVyMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1ujnILmAULVtVv3b
# /CDpM6RCdLV9Zjg+CDJFWLBzzjwAcHueV0mv4YgF4WoOhuc3o7GcIvl3P1DqxW97
# ex8cCfFcqdObZszKpP9OyeU5ft4c/rmfPC6PD2sKEWIIvLHAw/RXFS4RFoHngyGo
# 4070NFEMfFdQOSvBwHodsa128FG8hThRn8lXlWJG3327o39kLfawFAaCtfqEBVDd
# k4lYLl2aRpvuobfEATZ016qAHhxkExtuI007gGH58aokxpX+QWJI6T/Bj5eBO4Lt
# IqS6JjJdkRZPNc4Pa98OA+91nxoY5uZdrCrKReDeZ8qNZcyobgqAaCLtBS2esDFN
# 8HMByQIDAQABo4IBqTCCAaUwHwYDVR0jBBgwFoAUKZFg/4pN+uv5pmq4z/nmS71J
# zhIwHQYDVR0OBBYEFE+rkhTxw3ewJzXsZWbrdnRwy7y0MA4GA1UdDwEB/wQEAwIH
# gDAMBgNVHRMBAf8EAjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMDMBEGCWCGSAGG+EIB
# AQQEAwIEEDBGBgNVHSAEPzA9MDsGDCsGAQQBsjEBAgEDAjArMCkGCCsGAQUFBwIB
# Fh1odHRwczovL3NlY3VyZS5jb21vZG8ubmV0L0NQUzBDBgNVHR8EPDA6MDigNqA0
# hjJodHRwOi8vY3JsLmNvbW9kb2NhLmNvbS9DT01PRE9SU0FDb2RlU2lnbmluZ0NB
# LmNybDB0BggrBgEFBQcBAQRoMGYwPgYIKwYBBQUHMAKGMmh0dHA6Ly9jcnQuY29t
# b2RvY2EuY29tL0NPTU9ET1JTQUNvZGVTaWduaW5nQ0EuY3J0MCQGCCsGAQUFBzAB
# hhhodHRwOi8vb2NzcC5jb21vZG9jYS5jb20wGgYDVR0RBBMwEYEPcm9nZXJAemFu
# ZGVyLmNoMA0GCSqGSIb3DQEBCwUAA4IBAQBHs/5P4BiQqAuF83Z4R0fFn7W4lvfE
# 6KJOKpXajK+Fok+I1bDl1pVC9JIqhdMt3tdOFwvSl0/qQ9Sp2cZnMovaxT8Bhc7s
# +PDbzRlklGGRlnVg6i7RHnJ90bRdxPTFUBbEMLy7UAjQ4iPPfRoxaR4rzF3BLaaz
# b7BoGc/oEPIMo/WmXWFngeHAVQ6gVlr2WXrKwHo8UlN0jmgzR7QrD3ZHbhR4yRNq
# M97TgVp8Fdw3o+PnwMRj4RIeFiIr9KGockQWqth+W9CDRlTgnxE8MhKl1PbUGUFM
# DcG3cV+dFTI8P2/sYD+aQHdBr0nDT2RWSgeEchQ1s/isFwOVBrYEqqf7MIIF4DCC
# A8igAwIBAgIQLnyHzA6TSlL+lP0ct800rzANBgkqhkiG9w0BAQwFADCBhTELMAkG
# A1UEBhMCR0IxGzAZBgNVBAgTEkdyZWF0ZXIgTWFuY2hlc3RlcjEQMA4GA1UEBxMH
# U2FsZm9yZDEaMBgGA1UEChMRQ09NT0RPIENBIExpbWl0ZWQxKzApBgNVBAMTIkNP
# TU9ETyBSU0EgQ2VydGlmaWNhdGlvbiBBdXRob3JpdHkwHhcNMTMwNTA5MDAwMDAw
# WhcNMjgwNTA4MjM1OTU5WjB9MQswCQYDVQQGEwJHQjEbMBkGA1UECBMSR3JlYXRl
# ciBNYW5jaGVzdGVyMRAwDgYDVQQHEwdTYWxmb3JkMRowGAYDVQQKExFDT01PRE8g
# Q0EgTGltaXRlZDEjMCEGA1UEAxMaQ09NT0RPIFJTQSBDb2RlIFNpZ25pbmcgQ0Ew
# ggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCmmJBjd5E0f4rR3elnMRHr
# zB79MR2zuWJXP5O8W+OfHiQyESdrvFGRp8+eniWzX4GoGA8dHiAwDvthe4YJs+P9
# omidHCydv3Lj5HWg5TUjjsmK7hoMZMfYQqF7tVIDSzqwjiNLS2PgIpQ3e9V5kAoU
# GFEs5v7BEvAcP2FhCoyi3PbDMKrNKBh1SMF5WgjNu4xVjPfUdpA6M0ZQc5hc9IVK
# aw+A3V7Wvf2pL8Al9fl4141fEMJEVTyQPDFGy3CuB6kK46/BAW+QGiPiXzjbxghd
# R7ODQfAuADcUuRKqeZJSzYcPe9hiKaR+ML0btYxytEjy4+gh+V5MYnmLAgaff9UL
# AgMBAAGjggFRMIIBTTAfBgNVHSMEGDAWgBS7r34CPfqm8TyEjq3uOJjs2TIy1DAd
# BgNVHQ4EFgQUKZFg/4pN+uv5pmq4z/nmS71JzhIwDgYDVR0PAQH/BAQDAgGGMBIG
# A1UdEwEB/wQIMAYBAf8CAQAwEwYDVR0lBAwwCgYIKwYBBQUHAwMwEQYDVR0gBAow
# CDAGBgRVHSAAMEwGA1UdHwRFMEMwQaA/oD2GO2h0dHA6Ly9jcmwuY29tb2RvY2Eu
# Y29tL0NPTU9ET1JTQUNlcnRpZmljYXRpb25BdXRob3JpdHkuY3JsMHEGCCsGAQUF
# BwEBBGUwYzA7BggrBgEFBQcwAoYvaHR0cDovL2NydC5jb21vZG9jYS5jb20vQ09N
# T0RPUlNBQWRkVHJ1c3RDQS5jcnQwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmNv
# bW9kb2NhLmNvbTANBgkqhkiG9w0BAQwFAAOCAgEAAj8COcPu+Mo7id4MbU2x8U6S
# T6/COCwEzMVjEasJY6+rotcCP8xvGcM91hoIlP8l2KmIpysQGuCbsQciGlEcOtTh
# 6Qm/5iR0rx57FjFuI+9UUS1SAuJ1CAVM8bdR4VEAxof2bO4QRHZXavHfWGshqknU
# fDdOvf+2dVRAGDZXZxHNTwLk/vPa/HUX2+y392UJI0kfQ1eD6n4gd2HITfK7ZU2o
# 94VFB696aSdlkClAi997OlE5jKgfcHmtbUIgos8MbAOMTM1zB5TnWo46BLqioXwf
# y2M6FafUFRunUkcyqfS/ZEfRqh9TTjIwc8Jvt3iCnVz/RrtrIh2IC/gbqjSm/Iz1
# 3X9ljIwxVzHQNuxHoc/Li6jvHBhYxQZ3ykubUa9MCEp6j+KjUuKOjswm5LLY5TjC
# qO3GgZw1a6lYYUoKl7RLQrZVnb6Z53BtWfhtKgx/GWBfDJqIbDCsUgmQFhv/K53b
# 0CDKieoofjKOGd97SDMe12X4rsn4gxSTdn1k0I7OvjV9/3IxTZ+evR5sL6iPDAZQ
# +4wns3bJ9ObXwzTijIchhmH+v1V04SF3AwpobLvkyanmz1kl63zsRQ55ZmjoIs24
# 75iFTZYRPAmK0H+8KCgT+2rKVI2SXM3CZZgGns5IW9S1N5NGQXwH3c/6Q++6Z2H/
# fUnguzB9XIDj5hY5S6cxggI0MIICMAIBATCBkjB9MQswCQYDVQQGEwJHQjEbMBkG
# A1UECBMSR3JlYXRlciBNYW5jaGVzdGVyMRAwDgYDVQQHEwdTYWxmb3JkMRowGAYD
# VQQKExFDT01PRE8gQ0EgTGltaXRlZDEjMCEGA1UEAxMaQ09NT0RPIFJTQSBDb2Rl
# IFNpZ25pbmcgQ0ECEQDbJ+nktYWCvd7bDUv4jX83MAkGBSsOAwIaBQCgeDAYBgor
# BgEEAYI3AgEMMQowCKACgAChAoAAMBkGCSqGSIb3DQEJAzEMBgorBgEEAYI3AgEE
# MBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMCMGCSqGSIb3DQEJBDEWBBQi
# 7rdsVBqsHc5xeSnI2ximnv8r5DANBgkqhkiG9w0BAQEFAASCAQBmGsLHZVavtwmo
# xPi1aKNaMrUoIzAOFcKCiGBNVTZbk9gdasolOYpfPpretfrBz1bFby6h2px6B7oa
# pYUgV7CVVRp2MorH7H2k9wXDOo8Hym4E6d+evHUXM89hxqcjx/yTGYZVKUu42JPj
# srZOgRthK9kwsA+yWTw/ToJLqmym3EKp9QMLWHJNp5vxFhNE9Wq7xQf4IHM2XMXW
# LzYZmma1qJ3WaEAluxSBOgbvZYaGjEpr3vrl7jX43+yiSyYnHPygP0qUMZF7aptI
# a/aXVrCy0chjV/zpWfoQNQaNPMtWtn9lzW02ZEY1Yk7gJi3Lc62yFLa6MzwEYokb
# udClKQ65
# SIG # End signature block
