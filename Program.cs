using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

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
                        },
                        KeepRunning = true
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
    var subcnt = 1;
    foreach (var subProgramData in processData.SubPrograms) {
        Log($"[{cnt}.{subcnt}] Process: {subProgramData.ProcessName}");
        Log($"[{cnt}.{subcnt}] Program Path: {subProgramData.ProgramPath}");
        Log($"[{cnt}.{subcnt}] Arguments: {string.Join(" ", subProgramData.Arguments)}");
        Log($"[{cnt}.{subcnt}] Keep Running: {subProgramData.KeepRunning}");
        subcnt++;
    }
    cnt++;
}
Thread.Sleep(1000);
// Hide the console window
if (!config.LogToConsole) ShowWindow(GetConsoleWindow(), 0);


while (true) {
    foreach (var processData in config.Processes) {
        if (IsProcessRunning(processData.ProcessName)) {
            if (!lastSeenTimes.ContainsKey(processData.ProcessName)) {
                lastSeenTimes[processData.ProcessName] = DateTime.Now;
                Log($"{processData.ProcessName} was started at {lastSeenTimes[processData.ProcessName]}");
                foreach (var subProgramData in processData.SubPrograms) {
                    if (IsProcessRunning(subProgramData.ProcessName)) {
                        Log($"{subProgramData.ProcessName} is already running");
                        continue;
                    }
                    new Thread(() => {
                        if (subProgramData.Delay.HasValue) {
                            Log($"Waiting {subProgramData.Delay.Value.TotalSeconds} seconds before starting {subProgramData.ProcessName}");
                            Thread.Sleep(subProgramData.Delay.Value);
                            if (!IsProcessRunning(processData.ProcessName)) {
                                Log($"{processData.ProcessName} was closed while waiting for {subProgramData.ProcessName} to start");
                                return;
                            }
                            if (IsProcessRunning(subProgramData.ProcessName)) {
                                Log($"{subProgramData.ProcessName} is already running after delay");
                                return;
                            }
                        }
                        Log($"Starting {subProgramData.ProcessName}");
                        StartProcess(subProgramData.ProgramPath, subProgramData.Arguments);
                    }).Start();
                }
            } else {
                lastSeenTimes[processData.ProcessName] = DateTime.Now;
            }
        } else {
            if (lastSeenTimes.ContainsKey(processData.ProcessName) && DateTime.Now - lastSeenTimes[processData.ProcessName] > processData.GracePeriod) {
                Log($"{processData.ProcessName} was closed at {DateTime.Now}");
                foreach (var subProgramData in processData.SubPrograms) {
                    if (subProgramData.KeepRunning) continue;
                    foreach (var proc in Process.GetProcessesByName(subProgramData.ProcessName)) {
                        Log($"Closing {subProgramData.ProcessName}");
                        proc.CloseMainWindow();
                    }
                }
                lastSeenTimes.Remove(processData.ProcessName);
            }
        }
    }
    Thread.Sleep(config.CheckInterval);
}

[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

void Log(object obj) {
    var msg = $"[{DateTime.Now}] {obj}";
    Console.WriteLine(msg);
    if (config?.LogToFile == true) {
        File.AppendAllText("ProcessCombinator.log", msg + "\n");
    }
}

void StartProcess(string path, List<string> args) {
    Process.Start(new ProcessStartInfo(path, string.Join(" ", args)));
}

bool IsProcessRunning(string name) {
    return Process.GetProcessesByName(name).Length > 0;
}

public class MainConfig {
    public List<ProcessConfig> Processes { get; set; }
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool LogToConsole { get; set; } = false;
    public bool LogToFile { get; set; } = false;
}


public class ProcessConfig {
    public string ProcessName { get; set; }
    public TimeSpan? GracePeriod { get; set; } = TimeSpan.FromSeconds(5);
    public List<SubProgramData> SubPrograms { get; set; }
}

public class SubProgramData {
    [JsonIgnore]
    public string ProcessName => Path.GetFileNameWithoutExtension(ProgramPath);
    public string ProgramPath { get; set; }
    public List<string> Arguments { get; set; } = new List<string>();
    public bool KeepRunning { get; set; } = false;
    public TimeSpan? Delay { get; set; }
}