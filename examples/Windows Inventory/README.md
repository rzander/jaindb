JainDB can be used as an inventory database wit the following benefits:
* Agentless
* customizable PowerShell script to define the data you want to collect.
* Cloud or OnPremise
* History (all changes are tracked and stored in a blockchain)
* Reporting over PowerBi or Excel (or other Tools than can query data from REST API)
* Free and OpenSource

For testing, you can use the online Test-Instance: https://jaindb.azurewebsites.net/

To trigger an inventory, just run the following command (as admin):
```
Invoke-RestMethod -Uri "https://jaindb.azurewebsites.net/getps" | iex
```

The Command will get the inventory PowerShell Script and upload the result to: https://jaindb.azurewebsites.net/

If you want to query the uploaded data, you need a username and password (contat me on: roger at zander.ch).

>Note: https://jaindb.azurewebsites.net/ is just for demo or testing. All Data will be cleaned from time to time. 

>Note: Other users may be able to see your inventory! contact me if you want to have a "private" instance.

# Reporting
PowerBi or Excel are great Tools to connect with JainDB and visualize the results. You just have to connect to a "From Web" Datasource and enter the URL of the JainDB [REST API](https://github.com/rzander/jaindb/wiki/REST-API) query.

## Examples

### Device Summary
```
https://jaindb.azurewebsites.net/query?Computer;OS.Caption;OS.Version;OS.%23InstallDate;OS.OSArchitecture;OS.OSLanguage;Processor.Name
```
will show a summary of Hardware and OS Information:
```
    {
        "#id": "9qZbxVtx8JhmoQCM7oUpnbAJo",
        "#Name": "DESKTOP-25IV607",
        "#SerialNumber": "5943-9926-5433-6834-2729-5832-90",
        "#SMBIOSAssetTag": "5943-9926-5433-6834-2729-5832-90",
        "#UUID": "D26F533C-56AE-4478-ADBE-598B76EF240D",
        "ChassisTypes": [
            3
        ],
        "Domain": "WORKGROUP",
        "HypervisorPresent": true,
        "Manufacturer": "Microsoft Corporation",
        "Model": "Virtual Machine",
        "Roles": [
            "LM_Workstation",
            "LM_Server",
            "NT"
        ],
        "SystemFamily": "Virtual Machine",
        "SystemSKUNumber": "None",
        "TotalPhysicalMemory": 6000000000,
        "Version": "Hyper-V UEFI Release v2.5",
        "WakeUpType": 6,
        "OS.Caption": "Microsoft Windows 10 Pro",
        "OS.Version": "10.0.16299",
        "OS.#InstallDate": "2017-10-21T15:04:36Z",
        "#InstallDate": "2017-10-21T15:04:36Z",
        "OS.OSArchitecture": "64-bit",
        "OS.OSLanguage": 1033,
        "Processor.Name": "Intel(R) Core(TM) i7-6770HQ CPU @ 2.60GHz"
    }
```

### Installed Software on all Devices
```
https://jaindb.azurewebsites.net/query?Software&$select=%23Name;_date;%23id
```
will list all installed Software on all devices
```
        "#Name": "Client09",
        "_date": "2018-02-23T19:12:04.4552767Z",
        "#id": "9qZbCtRtSPHtdP23awWqtJEno",
        "Software": [
            {
                "@InstallDate": null,
                "DisplayName": "7-Zip 18.01 (x64)",
                "DisplayVersion": "18.01",
                "HelpLink": null,
                "Publisher": "Igor Pavlov",
                "UninstallString": "C:\\Program Files\\7-Zip\\Uninstall.exe"
            },
            ...
```

### Installed Hotfixes on all Devices
```
https://jaindb.azurewebsites.net/query?QFE&$exclude=QFE..@InstalledOn;QFE..Caption
```
will list all installed KB Articles from all devices
```
        "#id": "9qZbCtRqSPHtdP26awWqtJEno",
        "QFE": [
            {
                "Description": "Update",
                "HotFixID": "KB4057903"
            },
            {
                "Description": "Update",
                "HotFixID": "KB2959936"
            },
            ...
```
