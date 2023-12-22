﻿using System.Diagnostics;
using System.Text.Json;

const string appName = "Process Combinator v1.0.0";
const string configPath = @"ProcessCombinator.json";

Console.Title = appName;
Console.WriteLine($"{appName} started at {DateTime.Now}");
Console.WriteLine($"Config Path: \"{Path.GetFullPath(configPath)}\"");
if (!File.Exists(configPath)) {
    var cfg = new MainConfig {
        Processes = new List<ProcessConfig> {
            new ProcessConfig {
                ProcessName = "wordpad",
                GracePeriod = TimeSpan.FromSeconds(5),
                SubPrograms = new List<SubProgramData> {
                    new SubProgramData {
                        ProgramPath = @"C:\Windows\System32\notepad.exe",
                        Arguments = new List<string> {
                            @"C:\Users\user\Desktop\test.txt"
                        }
                    }
                }
            }
        }
    };
    File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions {
        WriteIndented = true
    }));
    Console.WriteLine($"Created \"{Path.GetFullPath(configPath)}\". Please edit it and restart the program.");
    Console.ReadKey();
    return;
}

string json = File.ReadAllText(configPath);
MainConfig config = JsonSerializer.Deserialize<MainConfig>(json);
Dictionary<string, DateTime> lastSeenTimes = new Dictionary<string, DateTime>();

Log($"Found {config.Processes.Count} processes. (Interval: {config.CheckInterval})");

var cnt = 1;
foreach (var processData in config.Processes) {
    Log($"[{cnt}] Process: {processData.ProcessName}");
    Log($"[{cnt}] Grace Period: {processData.GracePeriod}");
    foreach (var subProgramData in processData.SubPrograms) {
        Log($"[{cnt}]\tSub Program: {subProgramData.ProcessName}");
        Log($"[{cnt}]\tProgram Path: {subProgramData.ProgramPath}");
        Log($"[{cnt}]\tArguments: {string.Join(" ", subProgramData.Arguments)}");
    }
    Log(string.Empty);
    cnt++;
}

while (true) {
    foreach (var processData in config.Processes) {
        if (Process.GetProcessesByName(processData.ProcessName).Length > 0) {
            // if the process is not in the last seen times dictionary, add it and start its programs
            if (!lastSeenTimes.ContainsKey(processData.ProcessName)) {
                lastSeenTimes[processData.ProcessName] = DateTime.Now;
                Log($"{processData.ProcessName} was started at {lastSeenTimes[processData.ProcessName]}");
                foreach (var subProgramData in processData.SubPrograms) {
                    Process.Start(new ProcessStartInfo(subProgramData.ProgramPath, subProgramData.Arguments));
                }
            } else {
                // if the process is in the last seen times dictionary, update its last seen time
                lastSeenTimes[processData.ProcessName] = DateTime.Now;
                //Log($"{processData.ProcessName} is still running at {lastSeenTimes[processData.ProcessName]}");
            }
        } else {
            // If the process is not running and more than 5 seconds have passed since the last seen time, close the related program
            if (lastSeenTimes.ContainsKey(processData.ProcessName) && DateTime.Now - lastSeenTimes[processData.ProcessName] > processData.GracePeriod) {
                Log($"{processData.ProcessName} was closed at {DateTime.Now}");
                foreach (var subProgramData in processData.SubPrograms) {
                    foreach (var proc in Process.GetProcessesByName(subProgramData.ProcessName)) {
                        Log($"Closing {subProgramData.ProcessName}");
                        proc.CloseMainWindow();
                    }
                }
                // Remove the process from the last seen times dictionary
                lastSeenTimes.Remove(processData.ProcessName);
            }
        }
    }
    Thread.Sleep(config.CheckInterval);
}

void Log(object obj) {
    var msg = $"[{DateTime.Now}] {obj}";
    Console.WriteLine(msg);
    if (config?.LogToFile == true) {
        File.AppendAllText("ProcessCombinator.log", msg + "\n");
    }
}

public class MainConfig {
    public List<ProcessConfig> Processes { get; set; }
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool LogToFile { get; set; } = false;
}


public class ProcessConfig {
    public string ProcessName { get; set; }
    public TimeSpan? GracePeriod { get; set; } = TimeSpan.FromSeconds(5);
    public List<SubProgramData> SubPrograms { get; set; }
}

public class SubProgramData {
    public string ProcessName => Path.GetFileNameWithoutExtension(ProgramPath);
    public string ProgramPath { get; set; }
    public List<string> Arguments { get; set; }
}