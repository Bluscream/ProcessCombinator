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
          "ProcessName": "notepad",
          "ProgramPath": "C:\\Windows\\System32\\notepad.exe",
          "Arguments": [
            "C:\\Users\\user\\Desktop\\test.txt"
          ]
        }
      ]
    }
  ],
  "CheckInterval": "00:00:01",
  "LogToFile": false
}
```
