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
        $AppendObject.Value | Add-Member -MemberType NoteProperty -Name "#MAC" -Value (get-wmiobject -class "Win32_NetworkAdapterConfiguration" | Where {($_.IpEnabled -Match "True")}).MACAddress.Replace(':', '-')
        return $null
    }   
}


$object = New-Object PSObject
getinv -Name "Battery" -WMIClass "win32_Battery" -Properties @("BatteryStatus", "Caption", "Chemitry", "#Name", "Status", "PowerManagementCapabilities", "#DeviceID") -AppendObject ([ref]$object)
getinv -Name "BIOS" -WMIClass "win32_BIOS" -Properties @("Name", "Manufacturer", "Version", "#SerialNumber") -AppendObject ([ref]$object)
getinv -Name "Processor" -WMIClass "win32_Processor" -Properties @("Name", "Manufacturer", "Family", "NumberOfCores", "NumberOfEnabledCore", "NumberOfLogicalProcessors", "L2CacheSize", "L3CacheSize", "#ProcessorId") -AppendObject ([ref]$object)
getinv -Name "Memory" -WMIClass "win32_PhysicalMemory" -Properties @("Manufacturer", "ConfiguredClockSpeed", "ConfiguredVoltage", "PartNumber", "FormFactor", "DataWidth", "Speed", "SMBIOSMemoryType", "Name" , "Capacity" , "#SerialNumber") -AppendObject ([ref]$object)
getinv -Name "OS" -WMIClass "win32_OperatingSystem" -Properties @("BuildNumber", "BootDevice", "Caption", "CodeSet", "CountryCode", "@CurrentTimeZone", "EncryptionLevel", "Locale", "Manufacturer", "MUILanguages", "OperatingSystemSKU", "OSArchitecture", "OSLanguage", "SystemDrive", "Version", "#InstallDate", "@LastBootUpTime") -AppendObject ([ref]$object)

$CSP = getinv -Name "Computer" -WMIClass "win32_ComputerSystemProduct" -Properties @("#UUID", "Version")
$CS = getinv -Name "Computer" -WMIClass "win32_ComputerSystem" -Properties @("Domain", "HypervisorPresent", "InfraredSupported", "Manufacturer", "Model", "PartOfDomain", "Roles", "SystemFamily", "SystemSKUNumber", "#UserName", "WakeUpType", "TotalPhysicalMemory", "#Name") -AppendProperties $CSP 
getinv -Name "Computer" -WMIClass "win32_SystemEnclosure" -Properties @("ChassisTypes", "Model", "#SMBIOSAssetTag", "#SerialNumber") -AppendProperties $CS -AppendObject ([ref]$object)

getinv -Name "DiskDrive" -WMIClass "Win32_DiskDrive" -Properties @("@Capabilities", "Caption", "DeviceID", "@FirmwareRevision", "@Index", "InterfaceType", "MediaType", "Model", "@Partitions", "PNPDeviceID", "Size", "#SerialNumber" ) -AppendObject ([ref]$object)
getinv -Name "DiskPartition" -WMIClass "Win32_DiskPartition" -Properties @("BlockSize", "Bootable", "BootPartition", "DeviceID", "DiskIndex", "Index", "Size", "Type") -AppendObject ([ref]$object)

$ld = getinv -Name "LogicalDisk" -WMIClass "Win32_LogicalDisk" -Properties @("DeviceID", "DriveType", "FileSystem", "MediaType", "Size", "VolumeName", "@FreeSpace", "#VolumeSerialNumber") # -AppendObject ([ref]$object)
$ld = $ld | Where-Object { $_.DriveType -lt 4 }
$object | Add-Member -MemberType NoteProperty -Name "LogicalDisk" -Value ($ld)

getinv -Name "NetworkAdapter" -WMIClass "Win32_NetworkAdapter" -Properties @("AdapterType", "Description", "@DeviceID", "@InterfaceIndex", "#MACAddress", "Manufacturer", "PhysicalAdapter", "PNPDeviceID", "ServiceName", "@Speed") -AppendObject ([ref]$object)
getinv -Name "NetworkAdapterConfiguration" -WMIClass "Win32_NetworkAdapterConfiguration" -Properties @("@DefaultIPGateway", "Description", "DHCPEnabled", "#DHCPServer", "DNSDomain", "@Index", "@InterfaceIndex", "@IPAddress", "IPEnabled", "@IPSubnet", "#MACAddress" ) -AppendObject ([ref]$object)

getinv -Name "Video" -WMIClass "Win32_VideoController" -Properties @("AdapterCompatibility", "Name", "CurrentHorizontalResolution", "CurrentVerticalResolution", "CurrentBitsPerPixel", "CurrentRefreshRate", "DriverVersion", "InfSection", "PNPDeviceID", "VideoArchitecture", "VideoMemoryType") -AppendObject ([ref]$object)
getinv -Name "QFE" -WMIClass "Win32_QuickFixEngineering" -Properties @("Caption", "Description", "HotFixID", "@InstalledOn") -AppendObject ([ref]$object)
getinv -Name "Share" -WMIClass "Win32_Share" -Properties @("Name", "Description ", "Name", "Path", "Type") -AppendObject ([ref]$object)
getinv -Name "Audio" -WMIClass "Win32_SoundDevice" -Properties @("Caption", "Description ", "DeviceID", "Manufacturer") -AppendObject ([ref]$object)

getinv -Name "CDROM" -WMIClass "Win32_CDROMDrive" -Properties @("Capabilities", "Name", "Description", "DeviceID", "@Drive" , "MediaType") -AppendObject ([ref]$object)

#getinv -Name "Driver" -WMIClass "Win32_PnPSignedDriver" -Properties @("DeviceID","DeviceName", "DriverDate", "DriverProviderName", "DriverVersion", "FriendlyName", "HardWareID", "InfName" ) -AppendObject ([ref]$object)
getinv -Name "Printer" -WMIClass "Win32_Printer" -Properties @("DeviceID","CapabilityDescriptions","DriverName", "Local" , "Network", "PrinterPaperNames") -AppendObject ([ref]$object)



$user = Get-LocalUser | Select-Object Description, Enabled, UserMayChangePassword, PasswordRequired, Name, @{N = '@PasswordLastSet'; E = {[System.DateTime](($_.PasswordLastSet).ToUniversalTime())}}, @{N = 'id'; E = {$_.SID}} | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalUsers" -Value ($user)

$locAdmin = Get-LocalGroupMember -SID S-1-5-32-544 | Select-Object @{N = 'Name'; E = {$_.Name.Replace($($env:Computername) + "\", "")}}, PrincipalSource, ObjectClass | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalAdmins" -Value ($locAdmin)

$locGroup = Get-LocalGroup | Select-Object Description, Name, PrincipalSource, ObjectClass, @{N = 'id'; E = {$_.SID}}  | Sort-Object -Property Name
$object | Add-Member -MemberType NoteProperty -Name "LocalGroups" -Value ($locGroup)

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

#Services
$Services = get-service | Select-Object -ExcludeProperty MachineName, Site, Container, @{N = 'id'; E = { $_.Name}}
$object | Add-Member -MemberType NoteProperty -Name "Services" -Value ($Services )

#Cleanup
$object."LogicalDisk" | % { $_."@FreeSpace" = normalize($_."@FreeSpace")}
$object.Computer."TotalPhysicalMemory" = normalize($object.Computer."TotalPhysicalMemory")
#$object.NetworkAdapter | % { $_.Speed = normalize($_.Speed)}


SetID([ref] $object)

$id = $object."#id"
$con = $object | ConvertTo-Json -Compress
Write-Host "Device ID: $($id)"
Write-Host "Hash:" (Invoke-RestMethod -Uri "%LocalURL%:%WebPort%/upload/$($id)" -Method Post -Body $con -ContentType "application/json; charset=utf-8")