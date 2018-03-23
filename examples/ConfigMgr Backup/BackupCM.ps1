$jaindburi = "http://localhost:5000"
Import-Module (Join-Path $(Split-Path $env:SMS_ADMIN_UI_PATH) ConfigurationManager.psd1) 
$SiteCode = Get-PSDrive -PSProvider CMSITE
Push-Location "$($SiteCode.Name):\"

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

#Task-Sequences
Get-CMTaskSequence | ForEach-Object { 
    $object = New-Object PSObject
    $result  = New-Object PSObject
    $ts = $_
    $id = "ts-" + $_.PackageID
    $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $ts.Sequence -ContentType "application/json; charset=utf-8"
    $result  | Add-Member -MemberType NoteProperty -Name "#Name" -Value ("ts-" + $ts.Name)
    $object | Add-Member -MemberType NoteProperty -Name "sequence" -Value $js.sequence 
    $object | Add-Member -MemberType NoteProperty -Name "Name" -Value $ts.Name
    $object | Add-Member -MemberType NoteProperty -Name "PackageID" -Value $ts.PackageID 
    $object | Add-Member -MemberType NoteProperty -Name "BootImageID" -Value $ts.BootImageID 
    $object | Add-Member -MemberType NoteProperty -Name "Description" -Value $ts.Description
    $object | Add-Member -MemberType NoteProperty -Name "Duration" -Value $ts.Duration
    $object | Add-Member -MemberType NoteProperty -Name "EstimatedDownloadSizeMB" -Value $ts.EstimatedDownloadSizeMB
    $object | Add-Member -MemberType NoteProperty -Name "EstimatedRunTimeMinutes" -Value $ts.EstimatedRunTimeMinutes
    $object | Add-Member -MemberType NoteProperty -Name "@LastRefreshTime" -Value $ts.LastRefreshTime
    $object | Add-Member -MemberType NoteProperty -Name "ProgramFlags" -Value $ts.ProgramFlags
    $object | Add-Member -MemberType NoteProperty -Name "SecuredScopeNames" -Value $ts.SecuredScopeNames
    $object | Add-Member -MemberType NoteProperty -Name "SupportedOperatingSystems" -Value ($ts.SupportedOperatingSystems | ForEach-Object { $_.PropertyList })
    
    $result | Add-Member -MemberType NoteProperty -Name "TaskSequence" -Value $object
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($result| ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Applications
Get-CMApplication | ForEach-Object { 
    $object = New-Object PSObject
    $appl = New-Object PSObject 
    $id = "app-" + $_.CI_ID
    $app = $_
    $appl | Add-Member -MemberType NoteProperty -Name "#CI_ID" -Value $app.CI_ID
    $appl | Add-Member -MemberType NoteProperty -Name "#CI_UniqueID" -Value $app.CI_UniqueID
    $appl | Add-Member -MemberType NoteProperty -Name "#Name" -Value ("app-" + $app.LocalizedDisplayName)
    $object | Add-Member -MemberType NoteProperty -Name "CategoryInstance_UniqueIDs" -Value $app.CategoryInstance_UniqueIDs
    $object | Add-Member -MemberType NoteProperty -Name "CI_ID" -Value $app.CI_ID
    $object | Add-Member -MemberType NoteProperty -Name "PackageID" -Value $app.PackageID
    $object | Add-Member -MemberType NoteProperty -Name "CI_UniqueID" -Value $app.CI_UniqueID
    $object | Add-Member -MemberType NoteProperty -Name "CIVersion" -Value $app.CIVersion
    $object | Add-Member -MemberType NoteProperty -Name "CreatedBy" -Value $app.CreatedBy
    $object | Add-Member -MemberType NoteProperty -Name "DateCreated" -Value $app.DateCreated
    $object | Add-Member -MemberType NoteProperty -Name "@DateLastModified" -Value $app.DateLastModified
    $object | Add-Member -MemberType NoteProperty -Name "IsDeployed" -Value $app.IsDeployed
    $object | Add-Member -MemberType NoteProperty -Name "IsEnabled" -Value $app.IsEnabled
    $object | Add-Member -MemberType NoteProperty -Name "IsLatest" -Value $app.IsLatest
    $object | Add-Member -MemberType NoteProperty -Name "IsSuperseded" -Value $app.IsSuperseded
    $object | Add-Member -MemberType NoteProperty -Name "IsSuperseding" -Value $app.IsSuperseding
    $object | Add-Member -MemberType NoteProperty -Name "@LastModifiedBy" -Value $app.LastModifiedBy
    $object | Add-Member -MemberType NoteProperty -Name "LocalizedCategoryInstanceNames" -Value $app.LocalizedCategoryInstanceNames
    $object | Add-Member -MemberType NoteProperty -Name "LocalizedDisplayName" -Value $app.LocalizedDisplayName
    $object | Add-Member -MemberType NoteProperty -Name "ModelID" -Value $app.ModelID
    $object | Add-Member -MemberType NoteProperty -Name "ModelName" -Value $app.ModelName
    $object | Add-Member -MemberType NoteProperty -Name "SecuredScopeNames" -Value $app.SecuredScopeNames
    $object | Add-Member -MemberType NoteProperty -Name "@NumberOfDevicesWithApp" -Value $app.NumberOfDevicesWithApp
    $object | Add-Member -MemberType NoteProperty -Name "@NumberOfUsersWithApp" -Value $app.NumberOfUsersWithApp
    $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $app.SDMPackageXML -ContentType "application/json; charset=utf-8"
    $object | Add-Member -MemberType NoteProperty -Name "AppMgmtDigest" -Value $js.AppMgmtDigest
    
    $appl | Add-Member -MemberType NoteProperty -Name "App" -Value $object
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($appl  | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Collections
Get-CMCollection | ForEach-Object {
    $org = $_
    $coll = $org | Select-Object $org.PropertyNames -ExcludeProperty CollectionRules,MemberCount,LocalMemberCount,LastRefreshTime,LastMemberChangeTime
    $id = "coll-" + $coll.CollectionID
    $rules = @()
    $org.CollectionRules | ForEach-Object { $r = $_;$rules += $r | Select-Object $r.PropertyNames }
    $coll| Add-Member -MemberType NoteProperty -Name "CollectionRules" -Value $rules
    #$coll.CollectionRules = $coll.CollectionRules | Select-Object $coll.CollectionRules.PropertyNames
    $coll.RefreshSchedule = $coll.RefreshSchedule | Select-Object $coll.RefreshSchedule.PropertyNames
    $coll| Add-Member -MemberType NoteProperty -Name "@MemberCount" -Value $org.MemberCount
    $coll| Add-Member -MemberType NoteProperty -Name "@LocalMemberCount" -Value $org.LocalMemberCount
    $coll| Add-Member -MemberType NoteProperty -Name "@LastRefreshTime" -Value $org.LastRefreshTime
    $coll| Add-Member -MemberType NoteProperty -Name "@LastMemberChangeTime" -Value $org.LastMemberChangeTime
    $result  = New-Object PSObject
    $result | Add-Member -MemberType NoteProperty -Name "Collection" -Value $coll

    #CollectionSettings
    $settings = Get-CMCollectionSetting -CollectionID $coll.CollectionID | Select-Object AMTAutoProvisionEnabled, ClusterCount, ClusterPercentage, ClusterTimeout, CollectionID, CollectionVariablePrecedence, CollectionVariables, LastModificationTime, LocaleID, PollingInterval, PollingIntervalEnabled, PostAction, PowerConfigs, PreAction, RebootCountdown, RebootCountdownEnabled, RebootCountdownFinalWindow, ServiceWindows, SourceSite, UseCluster, UseClusterPercentage
    if ($settings) {
        $settings.CollectionVariables = $settings.CollectionVariables | Select-Object IsMasked, Name, Value
        $settings.ServiceWindows = $settings.ServiceWindows | Select-Object Description, Duration, IsEnabled, IsGMT, Name, RecurrenceType, ServiceWindowID, ServiceWindowSchedules, ServiceWindowType, StartTime 
        $settings.PowerConfigs = $settings.PowerConfigs | Select-Object ConfigID, DurationInSec, NonPeakPowerPlan, PeakPowerPlan, PeakStartTimeHoursMin, WakeUpTimeHoursMin
        $configID = $settings.PowerConfigs.ConfigID
        if ($configID) {
            $settings.PowerConfigs.PSObject.Properties.Remove('ConfigID') 
            $settings.PowerConfigs |  Add-Member -MemberType NoteProperty -Name "#ConfigID" -Value $ConfigID
        }
        $configID = $settings.CollectionID
        if ($configID) {
            $settings.PSObject.Properties.Remove('CollectionID')
            $settings |  Add-Member -MemberType NoteProperty -Name "#CollectionID" -Value $ConfigID
        }
        $lastMod = $settings.LastModificationTime
        if ($lastMod) {
            $settings.PSObject.Properties.Remove('LastModificationTime')
            $settings|  Add-Member -MemberType NoteProperty -Name "@LastModificationTime" -Value $lastMod
        }
        if($settings.PowerConfigs.NonPeakPowerPlan) {
            $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $settings.PowerConfigs.NonPeakPowerPlan -ContentType "application/json; charset=utf-8"
            $settings.PowerConfigs.NonPeakPowerPlan = $js
        }
        if($settings.PowerConfigs.PeakPowerPlan) {
            $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $settings.PowerConfigs.PeakPowerPlan -ContentType "application/json; charset=utf-8"
            $settings.PowerConfigs.PeakPowerPlan = $js
        }
    }
    
    $result | Add-Member -MemberType NoteProperty -Name "CollectionSettings" -Value $settings
    
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($result | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Packages
Get-CMPackage | ForEach-Object {
    $object = New-Object PSObject
    $pkg = $_
    $id = "pkg-" + $_.PackageID
    
    $prg = @($pkg | Get-CMProgram | Select-Object ProgramName, CommandLine, Comment, Description, DiskSpaceReq, Duration, DriveLetter, DependentProgram, ProgramFlags, SupportedOperatingSystems, MSIFilePath, MSIProductID, WorkingDirectory   )

    $prg | ForEach-Object {
        $p = $_
        $ver = $p.SupportedOperatingSystems | Select-Object MaxVersion, MinVersion, Name, Platform
        $p.PSObject.Properties.Remove("SupportedOperatingSystems")
        $p | Add-Member -MemberType NoteProperty -Name "SupportedOperatingSystems" -Value $ver
    }

    #Cleanup unused properties
    $pkg = $pkg | Select-Object $pkg.PropertyNames
    $lastRefresh = $pkg.LastRefreshTime
    $pkg.psobject.Properties.Remove("LastRefreshTime")
    $object | Add-Member -MemberType NoteProperty -Name "@LastRefreshTime" -Value $lastRefresh

    $object | Add-Member -MemberType NoteProperty -Name "Package" -Value $pkg
    $object | Add-Member -MemberType NoteProperty -Name "Programs" -Value $prg
    
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#BoundaryGroups
Get-CMBoundaryGroup | ForEach-Object { 
    $object = New-Object PSObject
    $id = "bg-" + $_.GroupID
    $bg = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "BoundaryGroup" -Value $bg
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Boundaries
Get-CMBoundary | ForEach-Object { 
    $object = New-Object PSObject
    $id = "bip-" + $_.BoundaryID
    $bg = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "Boundary" -Value $bg
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Boundary Relationship
Get-CMBoundaryGroupRelationship | ForEach-Object { 
    $object = New-Object PSObject
    $br = $_ | Select-Object $_.PropertyNames
    $id = GetMD5($br | ConvertTo-Json)
    
    $object | Add-Member -MemberType NoteProperty -Name "BoundaryRelationship" -Value $br
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#BootImages
Get-CMBootImage | ForEach-Object { 
    $object = New-Object PSObject
    $id = "boot-" + $_.PackageID
    $bi = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "BootImage" -Value $bi
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#ClientSettings
Get-CMClientSetting | ForEach-Object { 
    $object = New-Object PSObject
    $id = "cfg-" + $_.SettingsID
    $cfg = $_ | Select-Object $_.PropertyNames
    $cfgs = @()
    if ($cfg.AgentConfigurations) {
        $cfg.AgentConfigurations | ForEach-Object { 
            $acfg = New-Object PSObject
            $acfg | Add-Member -MemberType NoteProperty -Name "ClientSettings" -Value ($_ | Select-Object $_.PropertyNames )
            $cfgs += $acfg
        }
        $cfg.AgentConfigurations = $cfgs
    }
  
    $object | Add-Member -MemberType NoteProperty -Name "ClientSettings" -Value $cfg
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#ConfigItems
Get-CMConfigurationItem | ForEach-Object { 
    $object = New-Object PSObject
    $id = "ci-" + $_.CI_ID
    $orgobj = $_
    $ci = $_ | Select-Object $_.PropertyNames -ExcludeProperty SDMPackageXML,LocalizedInformation,EULASignoffDate
    $ci| Add-Member -MemberType NoteProperty -Name "@EULASignoffDate" -Value $orgobj.EULASignoffDate
    if ($orgobj.SDMPackageXML) {
        $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $orgobj.SDMPackageXML -ContentType "application/json; charset=utf-8"
        $ci| Add-Member -MemberType NoteProperty -Name "SDMPackageXML" -Value $js
    }
    $li = @()
    $orgobj.LocalizedInformation | ForEach-Object {
        $li += $_ | Select-Object $_.PropertyNames
    }
    $ci| Add-Member -MemberType NoteProperty -Name "LocalizedInformation" -Value $li
    $ci.SDMPackageLocalizedData = $ci.SDMPackageLocalizedData | Select-Object $ci.SDMPackageLocalizedData.PropertyNames
    $js2 = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $ci.SDMPackageLocalizedData.LocalizedData -ContentType "application/json; charset=utf-8"
    $ci.SDMPackageLocalizedData.psobject.Properties.Remove('LocalizedData') = $js2
    $ci.SDMPackageLocalizedData| Add-Member -MemberType NoteProperty -Name "LocalizedData" -Value $js2
    $ci.LocalizedCategoryInstanceNames = $ci.LocalizedCategoryInstanceNames | Select-Object $ci.LocalizedCategoryInstanceNames.PropertyNames
    $ci.LocalizedEulas = $ci.LocalizedEulas| Select-Object $ci.LocalizedEulas.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "ConfigItem" -Value $ci

    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Baselines
Get-CMBaseline | ForEach-Object { 
    $object = New-Object PSObject
    $id = "bl-" + $_.CI_ID
    $orgobj = $_
    $bi = $_ | Select-Object $_.PropertyNames -ExcludeProperty ActivatedCount, AssignedCount, ComplianceCount, CompliantPercentage, FailureCount, NonComplianceCount, SDMPackageXML
    $bi | Add-Member -MemberType NoteProperty -Name "@ActivatedCount" -Value $_.ActivatedCount
    $bi | Add-Member -MemberType NoteProperty -Name "@AssignedCount" -Value $_.AssignedCount
    $bi | Add-Member -MemberType NoteProperty -Name "@ComplianceCount" -Value $_.ComplianceCount
    $bi | Add-Member -MemberType NoteProperty -Name "@CompliantPercentage" -Value $_.CompliantPercentage
    $bi | Add-Member -MemberType NoteProperty -Name "@FailureCount" -Value $_.FailureCount
    $bi | Add-Member -MemberType NoteProperty -Name "@NonComplianceCount" -Value $_.NonComplianceCount
    $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body ($orgobj | Get-CMBaselineXMLDefinition) -ContentType "application/json; charset=utf-8"
    $bi | Add-Member -MemberType NoteProperty -Name "SDMPackageXML" -Value $js
    $object | Add-Member -MemberType NoteProperty -Name "Baseline" -Value $bi
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Site Server
Get-CMSiteSystemServer | ForEach-Object { 
    $object = New-Object PSObject
    $id = "srv-" + $_.NetworkOSPath.replace("\\", "")
    $srv = $_ | Select-Object $_.PropertyNames
    $props = @()
    $srv.Props = $srv.Props | % {
        $props += $_ | Select-Object $_.PropertyNames
    }
    $srv.psobject.Properties.remove('Props')
    $srv| Add-Member -MemberType NoteProperty -Name "Props" -Value $props
    $object | Add-Member -MemberType NoteProperty -Name "SiteSystemServer" -Value $srv
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#DP Groups
Get-CMDistributionPointGroup | ForEach-Object { 
    $object = New-Object PSObject
    $id = "dpg-" + $_.GroupID
    $dp = $_ | Select-Object $_.PropertyNames
    $dp | Add-Member -MemberType NoteProperty -Name "DistributionPoints" -Value (Get-CMDistributionPoint -DistributionPointGroupName ($dp.Name) | Select-Object NetworkOSPath)
    $object | Add-Member -MemberType NoteProperty -Name "DistributionPointGroup" -Value $dp
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#DP's
Get-CMDistributionPointInfo | ForEach-Object { 
    $object = New-Object PSObject
    $id = "dp-" + $_.ID
    $dp = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "DistributionPointInfo" -Value $dp
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Hierarchy Settings
Get-CMHierarchySetting | ForEach-Object { 
    $object = New-Object PSObject
    $id = "site-" + $_.SiteCode
    $site = $_ | Select-Object $_.PropertyNames
    $props = @()
    $site.Props = $site.Props | ForEach-Object {
        $props += $_ | Select-Object $_.PropertyNames
    }

    $proplist = {$props}.Invoke()
    $proplist.Remove(($proplist | Where-Object { $_.PropertyName -eq 'SiteControlHeartBeat' })) #Remove Heartbeat
    
    $site.psobject.Properties.remove('Props')
    $site | Add-Member -MemberType NoteProperty -Name "Props" -Value $proplist

    $props = @()
    $site.PropLists = $site.PropLists | ForEach-Object {
        $props += $_ | Select-Object $_.PropertyNames
    }
    $site.psobject.Properties.remove('PropLists')
    $site | Add-Member -MemberType NoteProperty -Name "PropLists" -Value $props

    $object | Add-Member -MemberType NoteProperty -Name "HierarchySetting" -Value $site

    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Site Maintenance Task 
Get-CMSiteMaintenanceTask | ForEach-Object { 
    $object = New-Object PSObject
    $id = "tsk-" + $_.ItemName
    $tsk = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "SiteMaintenanceTask" -Value $tsk
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#deployed SoftwareUpdate's
Get-CMSoftwareUpdate -Fast | Where-Object { $_.IsDeployed -eq $true }  | ForEach-Object { 
    $object = New-Object PSObject
    $id = "upd-" + $_.CI_ID
    $upd = $_ | Select-Object $_.PropertyNames -ExcludeProperty NumMissing, NumNotApplicable, NumPresent, NumTotal, NumUnknown, PercentCompliant, LastStatusTime
    $upd | Add-Member -MemberType NoteProperty -Name "@NumMissing" -Value $_.NumMissing
    $upd | Add-Member -MemberType NoteProperty -Name "@NumNotApplicable" -Value $_.NumNotApplicable
    $upd | Add-Member -MemberType NoteProperty -Name "@NumPresent" -Value $_.NumPresent
    $upd | Add-Member -MemberType NoteProperty -Name "@NumTotal" -Value $_.NumTotal
    $upd | Add-Member -MemberType NoteProperty -Name "@NumUnknown" -Value $_.NumUnknown
    $upd | Add-Member -MemberType NoteProperty -Name "@PercentCompliant" -Value $_.PercentCompliant
    $upd | Add-Member -MemberType NoteProperty -Name "@LastStatusTime" -Value $_.LastStatusTime
    $object | Add-Member -MemberType NoteProperty -Name "SoftwareUpdate" -Value $upd
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#UpdateGroup Deployments
Get-CMUpdateGroupDeployment | ForEach-Object { 
    $object = New-Object PSObject
    $id = "ugd-" + $_.AssignmentID
    $ugd = $_ | Select-Object $_.PropertyNames
    $object | Add-Member -MemberType NoteProperty -Name "UpdateGroupDeployment" -Value $ugd
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Device resources
Get-CMResource -ResourceType System -Fast | Where-Object { $_.Client -eq 1 } | ForEach-Object { 
    $object = New-Object PSObject
    $id = "res-" + $_.ResourceID
    $orgres = $_
    $object | Add-Member -MemberType NoteProperty -Name "#Name" -Value $_.Name
    $res = $_ | Select-Object $_.PropertyNames -ExcludeProperty AgentTime, LastLogonTimestamp
    $res | Add-Member -MemberType NoteProperty -Name "@AgentTime" -Value $_.AgentTime
    $res | Add-Member -MemberType NoteProperty -Name "@LastLogonTimestamp" -Value $_.LastLogonTimestamp
    $object | Add-Member -MemberType NoteProperty -Name "Device" -Value $res

    $vars = @()
    $orgres | Get-CMDeviceVariable | ForEach-Object {
        $vars += $_ | Select-Object $_.PropertyNames
    }

    $users = @()
    Get-CMUserDeviceAffinity -DeviceID $res.ResourceID | ForEach-Object {
        $users += $_ | Select-Object $_.PropertyNames
    }

    $object | Add-Member -MemberType NoteProperty -Name "DeviceVariable" -Value $vars
    $object | Add-Member -MemberType NoteProperty -Name "UserDeviceAffinity" -Value $users
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#OperatingSystem UpgradePackage
Get-CMOperatingSystemUpgradePackage | ForEach-Object { 
    $object = New-Object PSObject
    $id = "osd-" + $_.PackageID
    $osd = $_ | Select-Object $_.PropertyNames
    $osd.RefreshSchedule = $osd.RefreshSchedule | Select-Object $osd.RefreshSchedule.PropertyNames
    $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $osd.ImageProperty -ContentType "application/json; charset=utf-8"
    $osd.ImageProperty = $js
    $object | Add-Member -MemberType NoteProperty -Name "OperatingSystemUpgradePackage" -Value $osd
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#OperatingSystem Image
Get-CMOperatingSystemImage | ForEach-Object { 
    $object = New-Object PSObject
    $id = "osd-" + $_.PackageID
    $osd = $_ | Select-Object $_.PropertyNames
    $js = Invoke-RestMethod -Uri "$($jaindburi)/xml2json" -Method Post -Body $osd.ImageProperty -ContentType "application/json; charset=utf-8"
    $osd.ImageProperty = $js
    $object | Add-Member -MemberType NoteProperty -Name "OperatingSystemImage" -Value $osd
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

#Deployments
Get-CMDeployment | ForEach-Object { 
    $object = New-Object PSObject
    $org = $_
    $id = "depl-" + $_.DeploymentID
    $depl = $_ | Select-Object $_.PropertyNames -ExcludeProperty NumberErrors,NumberInProgress,NumberOther,NumberSuccess,NumberTargeted,NumberUnknown,SummarizationTime

    $depl | Add-Member -MemberType NoteProperty -Name "@NumberErrors" -Value $org.NumberErrors
    $depl | Add-Member -MemberType NoteProperty -Name "@NumberInProgress" -Value $org.NumberInProgress
    $depl | Add-Member -MemberType NoteProperty -Name "@NumberOther" -Value $org.NumberOther
    $depl | Add-Member -MemberType NoteProperty -Name "@NumberSuccess" -Value $org.NumberSuccess
    $depl | Add-Member -MemberType NoteProperty -Name "@NumberTargeted" -Value $org.NumberTargeted
    $depl | Add-Member -MemberType NoteProperty -Name "@NumberUnknown" -Value $org.NumberUnknown
    $depl | Add-Member -MemberType NoteProperty -Name "@SummarizationTime" -Value $org.SummarizationTime

    $object | Add-Member -MemberType NoteProperty -Name "Deployment" -Value $depl
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}
