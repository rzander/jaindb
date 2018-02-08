Import-Module (Join-Path $(Split-Path $env:SMS_ADMIN_UI_PATH) ConfigurationManager.psd1) 
$SiteCode = Get-PSDrive -PSProvider CMSITE
Push-Location "$($SiteCode.Name):\"
$namespace  = (Get-CMConnectionManager).NamedValueDictionary.connection

Get-CMTaskSequence | ForEach-Object { 
    $object = New-Object PSObject
    $ts = $_
    $id = "ts-" + $_.PackageID
    $js = Invoke-RestMethod -Uri "http://172.16.101.3:5000/xml2json" -Method Post -Body $ts.Sequence -ContentType "application/json; charset=utf-8"
    $object | Add-Member -MemberType NoteProperty -Name "sequence" -Value $js.sequence 
    $object | Add-Member -MemberType NoteProperty -Name "#Name" -Value ("ts-" + $ts.Name) 
    $object | Add-Member -MemberType NoteProperty -Name "BootImageID" -Value $ts.BootImageID 
    $object | Add-Member -MemberType NoteProperty -Name "Description" -Value $ts.Description
    $object | Add-Member -MemberType NoteProperty -Name "Duration" -Value $ts.Duration
    $object | Add-Member -MemberType NoteProperty -Name "EstimatedDownloadSizeMB" -Value $ts.EstimatedDownloadSizeMB
    $object | Add-Member -MemberType NoteProperty -Name "EstimatedRunTimeMinutes" -Value $ts.EstimatedRunTimeMinutes
    $object | Add-Member -MemberType NoteProperty -Name "@LastRefreshTime" -Value $ts.LastRefreshTime
    $object | Add-Member -MemberType NoteProperty -Name "ProgramFlags" -Value $ts.ProgramFlags
    $object | Add-Member -MemberType NoteProperty -Name "SecuredScopeNames" -Value $ts.SecuredScopeNames
    $object | Add-Member -MemberType NoteProperty -Name "SupportedOperatingSystems" -Value ($ts.SupportedOperatingSystems | ForEach-Object { $_.PropertyList })

    Invoke-RestMethod -Uri "http://172.16.101.3:5000/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

Get-CMApplication | ForEach-Object { 
    $object = New-Object PSObject
    $id = "app-" + $_.PackageID
    $app = $_
    $object | Add-Member -MemberType NoteProperty -Name "CategoryInstance_UniqueIDs" -Value $app.CategoryInstance_UniqueIDs
    $object | Add-Member -MemberType NoteProperty -Name "#CI_ID" -Value $app.CI_ID
    $object | Add-Member -MemberType NoteProperty -Name "#CI_UniqueID" -Value $app.CI_UniqueID
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
    $object | Add-Member -MemberType NoteProperty -Name "#LocalizedDisplayName" -Value ("app-" + $app.LocalizedDisplayName)
    $object | Add-Member -MemberType NoteProperty -Name "ModelID" -Value $app.ModelID
    $object | Add-Member -MemberType NoteProperty -Name "ModelName" -Value $app.ModelName
    $object | Add-Member -MemberType NoteProperty -Name "SecuredScopeNames" -Value $app.SecuredScopeNames
    $object | Add-Member -MemberType NoteProperty -Name "@NumberOfDevicesWithApp" -Value $app.LastModifiedBy
    $object | Add-Member -MemberType NoteProperty -Name "@NumberOfUsersWithApp" -Value $app.LastModifiedBy
    $js = Invoke-RestMethod -Uri "http://172.16.101.3:5000/xml2json" -Method Post -Body $app.SDMPackageXML -ContentType "application/json; charset=utf-8"
    $object | Add-Member -MemberType NoteProperty -Name "AppMgmtDigest" -Value $js.AppMgmtDigest


    Invoke-RestMethod -Uri "http://172.16.101.3:5000/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}


Get-CimInstance  -Namespace $namespace -ClassName "SMS_Collection" | ForEach-Object {
    $object = New-Object PSObject
    $id = "coll-" + $_.CollectionID
    $coll = $_ | Get-CimInstance 

    $object | Add-Member -MemberType NoteProperty -Name "CollectionID" -Value $coll.CollectionID
    $object | Add-Member -MemberType NoteProperty -Name "CollectionRules" -Value $coll.CollectionRules
    $object | Add-Member -MemberType NoteProperty -Name "CollectionType" -Value $coll.CollectionType
    $object | Add-Member -MemberType NoteProperty -Name "CollectionVariablesCount" -Value $coll.CollectionVariablesCount
    $object | Add-Member -MemberType NoteProperty -Name "Comment" -Value $coll.Comment
    $object | Add-Member -MemberType NoteProperty -Name "LimitToCollectionID" -Value $coll.LimitToCollectionID
    $object | Add-Member -MemberType NoteProperty -Name "LimitToCollectionName" -Value $coll.LimitToCollectionName
    $object | Add-Member -MemberType NoteProperty -Name "@MemberCount" -Value $coll.MemberCount
    $object | Add-Member -MemberType NoteProperty -Name "#Name" -Value ("coll-" + $coll.Name)
    $object | Add-Member -MemberType NoteProperty -Name "RefreshSchedule" -Value $coll.RefreshSchedule
    $object | Add-Member -MemberType NoteProperty -Name "RefreshType" -Value $coll.RefreshType
    $object | Add-Member -MemberType NoteProperty -Name "ServiceWindowsCount" -Value $coll.ServiceWindowsCount
    $object | Add-Member -MemberType NoteProperty -Name "UseCluster" -Value $coll.UseCluster
    $object | Add-Member -MemberType NoteProperty -Name "IsBuiltIn" -Value $coll.IsBuiltIn
    $object | Add-Member -MemberType NoteProperty -Name "PowerConfigsCount" -Value $coll.PowerConfigsCount
    $object | Add-Member -MemberType NoteProperty -Name "ReplicateToSubSites" -Value $coll.ReplicateToSubSites
    $object | Add-Member -MemberType NoteProperty -Name "IncludeExcludeCollectionsCount" -Value $coll.IncludeExcludeCollectionsCount

    #Cleanup unused properties
    $object.CollectionRules = $object.CollectionRules | Select-Object * -ExcludeProperty Cim*, PSComp*
    $object.RefreshSchedule = $object.RefreshSchedule | Select-Object * -ExcludeProperty Cim*, PSComp*

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
    }
    
    $object | Add-Member -MemberType NoteProperty -Name "CollectionSettings" -Value $settings 

    Invoke-RestMethod -Uri "http://172.16.101.3:5000/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

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
    $pkg | Add-Member -MemberType NoteProperty -Name "Programs" -Value $prg
    $object | Add-Member -MemberType NoteProperty -Name "Package" -Value $pkg
    

    Invoke-RestMethod -Uri "http://172.16.101.3:5000/upload/$($id)" -Method Post -Body ($object | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8" 
}

