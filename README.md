# ProcessCombinator

## Example config
```json
{
  "Processes": [
    {
      "ProcessName": "vrchat",
      "GracePeriod": "00:01:00",
      "SubPrograms": [
        {
          "ProgramPath": "C:\\Program Files\\VRCX\\VRCX.exe",
          "KeepRunning": true
        },
        {
          "ProgramPath":"C:\\Users\\blusc\\AppData\\Local\\VRCOSC\\VRCOSC.exe"
        }
      ]
    },
    {
      "ProcessName": "vrcx",
      "GracePeriod": "00:00:05",
      "SubPrograms": [
        {
          "ProgramPath": "D:\\OneDrive\\Games\\VRChat\\_TOOLS\\VRCX\\_TOOLS\\StandaloneNotifier.exe",
          "Delay": "00:00:30"
        }
      ]
    },
    {
      "ProcessName": "vrserver",
      "GracePeriod": "00:00:30",
      "SubPrograms": []
    }
  ],
  "CheckInterval": "00:00:01",
  "LogToConsole": false,
  "LogToFile": false
}
```
