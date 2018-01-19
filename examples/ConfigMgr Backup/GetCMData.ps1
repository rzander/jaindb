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

$object = New-Object PSObject
$site = Get-CMSiteDefinition
$id = GetMD5($site.SiteCode + $site.SiteServerDomain)
$object | Add-Member -MemberType NoteProperty -Name "#Id" -Value $id 
$object | Add-Member -MemberType NoteProperty -Name "#Name" -Value $site.SiteName
$object | Add-Member -MemberType NoteProperty -Name "#Site" -Value $site.SiteCode
$object | Add-Member -MemberType NoteProperty -Name "#SiteServer" -Value $site.SiteServerName

#Devices
$res =  Get-CMResource -ResourceType System -Fast | Where-Object { $_.Client -eq 1 } | Sort-Object Name | Select-Object ADSiteName,AgentName,AgentSite, Build,ClientVersion,CPUType,CreationDate,DistinguishedName,FullDomainName,HardwareID,IsVirtualMachine,LastLogonUserName,MACAddresses, Name,OperatingSystemNameandVersion,ResourceDomainORWorkgroup,ResourceId,SID,SMSAssignedSites,SMSUniqueIdentifier,VirtualMachineHostName
$object | Add-Member -MemberType NoteProperty -Name "Devices" -Value $res
$resVar = Get-CMResource -ResourceType System -Fast | Where-Object { $_.Client -eq 1 } | ForEach-Object { $a = (Get-CMDeviceVariable -ResourceID $_.ResourceID | Select-Object IsMasked,Name,value); $a | Add-Member -MemberType NoteProperty -Name 'DeviceName' -Value $_.Name; $a | Add-Member -MemberType NoteProperty -Name 'ResourceID' -Value $_.ResourceID;  $a  }
$object | Add-Member -MemberType NoteProperty -Name "DeviceVariables" -Value $resVar
$resUser = Get-CMResource -ResourceType System -Fast | Where-Object { $_.Client -eq 1 } | ForEach-Object { Get-CMUserDeviceAffinity -DeviceId $_.ResourceID | Select-Object CreationTime,IsActive,RelationshipResourceID,ResourceClientType,ResourceID,ResourceName,Sources,Types,UniqueUserName }
$object | Add-Member -MemberType NoteProperty -Name "DeviceAffinity" -Value $resUser

#Collections
$directRules = Get-CMCollection | ForEach-Object { $a =  ($_ | Get-CMCollectionDirectMembershipRule | Select-Object ResourceClassName,ResourceID,RuleName ); $a | Add-Member -MemberType NoteProperty -Name 'CollectionID' -Value $_.CollectionID; $a }
$object | Add-Member -MemberType NoteProperty -Name "DirectRules" -Value $directRules
$coll = (Get-CMCollection) | Sort-Object CollectionID |Select-Object ((Get-CMCollection)[0].PropertyNames) -ExcludeProperty LastChangeTime, LastRefreshTime, LastMemberChangeTime
$object | Add-Member -MemberType NoteProperty -Name "Collections" -Value $coll
$collSettings = Get-CMDeviceCollection | Get-CMCollectionSetting | Sort-Object CollectionID | Select-Object AMTAutoProvisionEnabled,ClusterCount,ClusterPercentage,ClusterTimeout,CollectionID,CollectionVariablePrecedence,CollectionVariables,LastModificationTime,LocaleID,PollingInterval,PollingIntervalEnabled,PostAction,PowerConfigs,PreAction,RebootCountdown,RebootCountdownEnabled,RebootCountdownFinalWindow,ServiceWindows,SourceSite,UseCluster,UseClusterPercentage
$object | Add-Member -MemberType NoteProperty -Name "CollectionSettings" -Value $collSettings

#Applications
$apps = Get-CMApplication | Sort-Object ApplicationName | Select-Object ((Get-CMApplication | Select-Object -First 1).PropertyNames) -ExcludeProperty SDMPackageXML, SummarizationTime, NumberOfDevicesWithApp, NumberOfDevicesWithFailure, NumberOfUsersWithApp, NumberOfUsersWithFailure, NumberOfUsersWithRequest
$object | Add-Member -MemberType NoteProperty -Name "Applications" -Value $apps
$appdepl = Get-CMApplicationDeployment | Sort-Object AssignmentUniqueID | Select-Object ((Get-CMApplicationDeployment | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "ApplicationDeployments" -Value $appdepl
$props = ((Get-CMDeploymentType -ApplicationName * | Select-Object -First 1).PropertyNames)
$DT = Get-CMDeploymentType -ApplicationName *  | Select-Object ($props) -ExcludeProperty SDMPackageXML
$object | Add-Member -MemberType NoteProperty -Name "DeploymentType" -Value $DT

#Packages
$pkg = Get-CMPackage | Sort-Object PackageID | Select-Object ((Get-CMPackage  | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "Packages" -Value $pkg
$prog = Get-CMProgram | Sort-Object PackageID | Select-Object ((Get-CMProgram  | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "Programs" -Value $prog

#Deployed Updates
$updates = Get-CMSoftwareUpdate -Fast | Where-Object { $_.IsDeployed -eq $true } | Select-Object ArticleID,BulletinID,CI_ID,CI_UniqueID,DatePosted,DateRevised,IsDeployed,IsExpired,IsSuperseded,LocalizedDisplayName,ModelID,ModelName,PercentCompliant
$object | Add-Member -MemberType NoteProperty -Name "Updates" -Value $updates

#UpdateGroups
$upd = Get-CMUpdateGroupDeployment | Sort-Object AssignmentName | Select-Object ((Get-CMUpdateGroupDeployment | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "UpdateGroupDeployments" -Value $upd

#Boundary
$bdg = Get-CMBoundaryGroup | Sort-Object Name | Select-Object ((Get-CMBoundaryGroup | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "BoundaryGroups" -Value $bdg
$bd = Get-CMBoundary| Sort-Object BoundaryID | Select-Object ((Get-CMBoundary | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "Boundary" -Value $bd
$bdr = Get-CMBoundaryGroupRelationship| Sort-Object DestinationGroupName | Select-Object ((Get-CMBoundaryGroupRelationship | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "BoundaryRelationships" -Value $bdr

#Boot Images
$boot = Get-CMBootImage | Sort-Object Name | Select-Object ((Get-CMBootImage | Select-Object -First 1).PropertyNames)
$object | Add-Member -MemberType NoteProperty -Name "BootImages" -Value $boot

#Configuration Items
$ci = Get-CMConfigurationItem -Fast | Sort-Object Name | Select-Object ((Get-CMConfigurationItem -Fast | Select-Object -First 1).PropertyNames) -ExcludeProperty SDMPackageXML
$object | Add-Member -MemberType NoteProperty -Name "ConfigurationItems" -Value $ci

#ClientSettings
$clientSettings = Get-CMClientSetting | ForEach-Object { $a = ($_ |  Select-Object AssignmentCount,CreatedBy,DateCreated,DateModified,Description,Enabled,Flags,LastModifiedBy,Name,Priority,SettingsID,SiteCode,Type,UniqueID); $a | Add-Member -MemberType NoteProperty -Name 'Properties' -Value $_.Properties.tostring(); $a}
$object | Add-Member -MemberType NoteProperty -Name "ClientSettings" -Value $clientSettings

#Site Servers
$siteServers = Get-CMSiteSystemServer | ForEach-Object { $a = ($_ | Select-Object FileType,ItemName,ItemType,NALPath,NALType,NetworkOSPath,PropLists,Props,RoleCount,RoleName,ServerState,ServiceWindows,SiteCode,SiteSystemStatus,SslState,Type); $a | Add-Member -MemberType NoteProperty -Name 'Properties' -Value $_.Properties.tostring(); $a}
$object | Add-Member -MemberType NoteProperty -Name "SiteServers" -Value $siteServers

#DP Groups
$dpGroups = Get-CMDistributionPointGroup | Select-Object CollectionCount,ContentCount,ContentInSync,CreatedBy,CreatedOn,Description,GroupID,HasMember,HasRelationship,MemberCount,ModifiedBy,ModifiedOn,Name,OutOfSyncContentCount,SourceSite
$object | Add-Member -MemberType NoteProperty -Name "DPGroups" -Value $dpGroups

#DP's
$DP = Get-CMDistributionPointInfo | Select-Object AddressScheduleEnabled,BindExcept,BindPolicy,BitsEnabled,CertificateType,Communication,Description,DPFlags,Drive,GroupCount,HasRelationship,HealthCheckEnabled,HealthCheckPriority,HealthCheckSchedule,ID,IdentityGUID,InternetFacing,IsActive,IsMulticast,IsPeerDP,IsProtected,IsPullDP,IsPXE,NALPath,Name,OperatingSystem,PreStagingAllowed,Priority,PXEPassword,RateLimitsEnabled,Region,ResourceType,ResponseDelay,SccmPXE,ServerName,ServiceType,ShareName,SiteCode,SiteName,SupportUnknownMachines,TransferRate,UdaSetting,Version
$object | Add-Member -MemberType NoteProperty -Name "DP" -Value $DP

#Hierarchy Settings
$site = Get-CMHierarchySetting | Select-Object AddressPublicKey,FileType,InstallDirectory,ItemName,ItemType,ParentSiteCode,PropLists,Props,ServiceAccount,ServiceAccountDomain,ServiceAccountPassword,ServiceExchangeKey,ServicePlaintextAccount,ServicePublicKey,SiteCode,SiteName,SiteServerDomain,SiteServerName,SiteServerPlatform,SiteType,SQLAccount,SQLAccountPassword,SQLDatabaseName,SQLPublicKey,SQLServerName
$object | Add-Member -MemberType NoteProperty -Name "SiteHierarchy" -Value $site

#Maintenance Tasks
$tasks = Get-CMSiteMaintenanceTask | Select-Object ItemName,ItemType,BeginTime, DaysOfWeek,DeleteOlderThan,Enabled,TaskType
$object | Add-Member -MemberType NoteProperty -Name "SiteMaintenance" -Value $tasks

#$object  | ConvertTo-Json >c:\temp\SCCM.json -Compress

#Invoke-RestMethod -Uri "http://localhost:5000/UploadFullInv/$($id)" -Method Post -Body ($object  | ConvertTo-Json -Compress) -ContentType 'application/json'