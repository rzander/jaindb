$ccu = "192.168.2.15"
$user = "Admin"
$pw = "xxxx"
$uri = "http://" + $ccu + "/api/homematic.cgi"
$jaindburi = "http://localhost:5000"

$getLogin = '{ "method": "Session.login", "params":  { "username" : "' + $user + '", "password" : "' + $pw + '" }}'
$SessionID = (Invoke-RestMethod -uri $uri -Body $getLogin -Method Post).result

$getDeviceAll = '{ "method": "Device.listAllDetail", "params": { "_session_id_" : "' + $SessionID + '" }}'
$DeviceList = (Invoke-RestMethod -uri $uri -Body $getDeviceAll -Method Post).result
$DeviceList | ForEach-Object { $id = $_.id;
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($_ | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8"
} 

$getRoomsAll = '{ "method": "Room.getAll", "params": { "_session_id_" : "' + $SessionID + '" }}'
$RoomList = (Invoke-RestMethod -uri $uri -Body $getRoomsAll -Method Post).result
$RoomList | ForEach-Object { $id = $_.id;
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($_ | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8"
} 

$getFunctionsAll = '{ "method": "Subsection.getAll", "params": { "_session_id_" : "' + $SessionID + '" }}'
$FunctionsList = (Invoke-RestMethod -uri $uri -Body $getFunctionsAll -Method Post).result
$FunctionsList | ForEach-Object { $id = $_.id;
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($_ | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8"
} 

$getVariablesAll = '{ "method": "SysVar.getAll", "params": { "_session_id_" : "' + $SessionID + '" }}'
$VariablesList = (Invoke-RestMethod -uri $uri -Body $getVariablesAll -Method Post).result
$VariablesList | ForEach-Object { $id = $_.id;
    Invoke-RestMethod -Uri "$($jaindburi)/upload/$($id)" -Method Post -Body ($_ | ConvertTo-Json -Compress -Depth 10) -ContentType "application/json; charset=utf-8"
} 