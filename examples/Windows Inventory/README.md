JainDB can be used as an inventory database. 
For testing, you can use the online Test-Instance: https://jaindb.azurewebsites.net/

To trigger an inventory, just run the following command (as admin):
```
Invoke-RestMethod -Uri "https://jaindb.azurewebsites.net/getps" | iex
```

The Command will get the inventory PowerShell Script and upload the result to: https://jaindb.azurewebsites.net/

If you want to query the uploaded data, you need a username and password (contat me on: roger at zander.ch).

>Note: https://jaindb.azurewebsites.net/ is just for demo or testing. All Data will be cleaned from time to time. 

>Note: Other users may be able to see your inventory! contact me if you want to have a "private" instance.
