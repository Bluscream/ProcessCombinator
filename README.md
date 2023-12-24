# ProcessCombinator

## Example config
```json
{
  "Processes": [
    {
      "ProcessName": "wordpad",
      "GracePeriod": "00:00:05",
      "SubPrograms": [
        {
          "ProgramPath": "C:\\Windows\\System32\\notepad.exe",
          "Arguments": [
            "C:\\Users\\user\\Desktop\\test.txt"
          ],
          "KeepRunning": true
        }
      ]
    }
  ],
  "CheckInterval": "00:00:01",
  "LogToConsole": false,
  "LogToFile": false
}
```
