using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

const string appName = "Process Combinator";
const string configPath = @"ProcessCombinator.json";

Console.Title = appName;
Console.WriteLine($"{appName} started at {DateTime.Now}");
Console.WriteLine($"Config Path: \"{Path.GetFullPath(configPath)}\"");
if (!File.Exists(configPath))
{
    var cfg = new MainConfig
    {
        Processes = new List<ProcessConfig>
        {
            new ProcessConfig
            {
                ProcessName = "wordpad",
                GracePeriod = TimeSpan.FromSeconds(5),
                SubPrograms = new List<SubProgramData>
                {
                    new SubProgramData
                    {
                        ProgramPath = @"C:\Windows\System32\notepad.exe",
                        WorkingDirectory = @"C:\Windows\System32\",
                        Arguments = new List<string> { @"C:\Users\user\Desktop\test.txt" },
                        KeepRunning = true,
                        Delay = TimeSpan.FromSeconds(1),
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        AlwaysRun = true,
                    },
                },
            },
        },
        CaseInsensitive = false,
    };
    File.WriteAllText(
        configPath,
        JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true })
    );
    Console.WriteLine(
        $"Created \"{Path.GetFullPath(configPath)}\". Please edit it and restart the program."
    );
    Console.ReadKey();
    return;
}

MainConfig config;
try
{
    string json = File.ReadAllText(configPath);
    config = JsonSerializer.Deserialize<MainConfig>(json);
    if (config is null)
        throw new ArgumentNullException("config");

    // Remove .exe extensions from process names and show warnings
    foreach (var processData in config.Processes)
    {
        if (processData.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Log(
                $"Warning: Process name '{processData.ProcessName}' ends with '.exe'. Removing extension for process detection."
            );
            processData.ProcessName = processData.ProcessName.TrimEnd(".exe".ToCharArray());
        }
    }
}
catch (Exception ex)
{
    // Unhide console and show error
    ShowWindow(GetConsoleWindow(), 1);
    Console.WriteLine("Error loading configuration");
    Console.WriteLine(Path.GetFullPath(configPath));
    Console.WriteLine(ex.Message);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
    return;
}

Dictionary<string, DateTime> lastSeenTimes = new Dictionary<string, DateTime>();

Log($"Found {config.Processes.Count} processes. (Interval: {config.CheckInterval})");

var cnt = 1;
foreach (var processData in config.Processes)
{
    Log($"[{cnt}] Process: {processData.ProcessName}");
    Log($"[{cnt}] Grace Period: {processData.GracePeriod}");
    var subcnt = 1;
    foreach (var subProgramData in processData.SubPrograms)
    {
        Log($"[{cnt}.{subcnt}] Process: {subProgramData.ProcessName}");
        Log(
            $"[{cnt}.{subcnt}] Program Path: \"{subProgramData.ProgramPath}\" ({(subProgramData.ParsedPath.Exists ? "valid" : "invalid")})"
        );
        Log($"[{cnt}.{subcnt}] Arguments: {string.Join(" ", subProgramData.Arguments)}");
        Log($"[{cnt}.{subcnt}] Keep Running: {subProgramData.KeepRunning}");
        Log($"[{cnt}.{subcnt}] Always Run: {subProgramData.AlwaysRun}");
        Log($"[{cnt}.{subcnt}] Delay: {subProgramData.Delay}");
        subcnt++;
    }
    cnt++;
}
Thread.Sleep(1000);

// Hide the console window
if (!config.LogToConsole)
{
    ShowWindow(GetConsoleWindow(), 0);
}

while (true)
{
    try
    {
        foreach (var processData in config.Processes)
        {
            if (IsProcessRunning(processData.ProcessName))
            {
                if (!lastSeenTimes.ContainsKey(processData.ProcessName))
                {
                    lastSeenTimes[processData.ProcessName] = DateTime.Now;
                    Log(
                        $"{processData.ProcessName} was started at {lastSeenTimes[processData.ProcessName]}"
                    );
                    foreach (var subProgramData in processData.SubPrograms)
                    {
                        if (
                            IsProcessRunning(subProgramData.ProcessName)
                            && !subProgramData.AlwaysRun
                        )
                        {
                            Log(
                                $"{subProgramData.ProcessName} is already running, use the 'AlwaysRun' config key if this is intentended!"
                            );
                            continue;
                        }
                        new Thread(() =>
                        {
                            try
                            {
                                if (
                                    subProgramData.Delay.HasValue
                                    && subProgramData.Delay.Value.TotalMilliseconds > 0
                                )
                                {
                                    Log(
                                        $"Waiting {subProgramData.Delay.Value.TotalSeconds} seconds before starting {subProgramData.ProcessName}"
                                    );
                                    Thread.Sleep(subProgramData.Delay.Value);
                                    if (!IsProcessRunning(processData.ProcessName))
                                    {
                                        Log(
                                            $"{processData.ProcessName} was closed while waiting for {subProgramData.ProcessName} to start"
                                        );
                                        return;
                                    }
                                    if (IsProcessRunning(subProgramData.ProcessName))
                                    {
                                        Log(
                                            $"{subProgramData.ProcessName} is already running after delay"
                                        );
                                        return;
                                    }
                                }
                                Log($"Starting {subProgramData.ProcessName}");
                                var workDir =
                                    subProgramData.WorkingDirectory
                                    ?? subProgramData.ParsedPath?.Directory?.FullName
                                    ?? null;
                                StartProcessFromFile(
                                    path: subProgramData.ParsedPath,
                                    args: subProgramData.Arguments,
                                    workDir: workDir,
                                    noWindow: subProgramData.CreateNoWindow,
                                    shellExecute: subProgramData.UseShellExecute
                                );
                            }
                            catch (Exception ex)
                            {
                                Log($"Exception in subprogram thread: {ex}");
                            }
                        }).Start();
                    }
                }
                else
                {
                    lastSeenTimes[processData.ProcessName] = DateTime.Now;
                }
            }
            else
            {
                if (
                    lastSeenTimes.ContainsKey(processData.ProcessName)
                    && DateTime.Now - lastSeenTimes[processData.ProcessName]
                        > processData.GracePeriod
                )
                {
                    Log($"{processData.ProcessName} was closed at {DateTime.Now}");
                    foreach (var subProgramData in processData.SubPrograms)
                    {
                        if (subProgramData.KeepRunning)
                            continue;
                        foreach (var proc in Process.GetProcessesByName(subProgramData.ProcessName))
                        {
                            new Thread(() =>
                            {
                                try
                                {
                                    Log($"Closing {subProgramData.ProcessName}");
                                    var t = Task.Run(() =>
                                    {
                                        proc.CloseMainWindow();
                                    });
                                    if (t.Wait(500))
                                    {
                                        if (IsProcessRunning(subProgramData.ProcessName))
                                        {
                                            Log(
                                                $"{subProgramData.ProcessName} did not close main window in time, closing process!"
                                            );
                                            t = Task.Run(() =>
                                            {
                                                proc.Close();
                                            });
                                            if (t.Wait(250))
                                            {
                                                if (IsProcessRunning(subProgramData.ProcessName))
                                                {
                                                    Log(
                                                        $"{subProgramData.ProcessName} did not close in time, killing process!"
                                                    );
                                                    t = Task.Run(() =>
                                                    {
                                                        proc.Kill();
                                                    });
                                                    if (t.Wait(250))
                                                    {
                                                        if (
                                                            IsProcessRunning(
                                                                subProgramData.ProcessName
                                                            )
                                                        )
                                                        {
                                                            Log(
                                                                $"{subProgramData.ProcessName} did not die in time, ignoring!"
                                                            );
                                                            return;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Log($"Closed {subProgramData.ProcessName}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log(ex.Message);
                                    StartProcess(
                                        "taskkill",
                                        new() { "/f", "/im", subProgramData.ParsedPath.Name },
                                        noWindow: true
                                    );
                                }
                            }).Start();
                        }
                    }
                    lastSeenTimes.Remove(processData.ProcessName);
                }
            }
        }
    }
    catch (Exception ex)
    {
        ShowWindow(GetConsoleWindow(), 1);
        Log($"Exception in main loop: {ex}");
        // Console.WriteLine("\nPress any key to continue...");
        // Console.ReadKey();
    }
    Thread.Sleep(config.CheckInterval);
}

[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

void Log(object obj)
{
    var msg = $"[{DateTime.Now}] {obj}";
    Console.WriteLine(msg);
    if (config?.LogToFile == true)
    {
        File.AppendAllText("ProcessCombinator.log", msg + "\n");
    }
}
void StartProcessFromFile(
    FileInfo path,
    List<string> args,
    string? workDir = null,
    bool noWindow = false,
    bool shellExecute = false
) => StartProcess(path.FullName, args, workDir, noWindow, shellExecute);
void StartProcess(
    string path,
    List<string> args,
    string? workDir = null,
    bool noWindow = false,
    bool shellExecute = false
)
{
    var proc = new ProcessStartInfo(path, string.Join(" ", args))
    {
        WorkingDirectory = workDir,
        UseShellExecute = shellExecute,
        CreateNoWindow = noWindow,
    };
    Log($"Running \"{path}\" {proc.Arguments}");
    Process.Start(proc);
}

bool IsProcessRunning(string name)
{
    if (config.CaseInsensitive)
    {
        return Process
            .GetProcesses()
            .Any(p => string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase));
    }
    return Process.GetProcessesByName(name).Length > 0;
}

public class MainConfig
{
    public List<ProcessConfig> Processes { get; set; }
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool LogToConsole { get; set; } = false;
    public bool LogToFile { get; set; } = false;
    public bool CaseInsensitive { get; set; } = false;
}

public class ProcessConfig
{
    public string ProcessName { get; set; }
    public TimeSpan? GracePeriod { get; set; } = TimeSpan.FromSeconds(5);
    public List<SubProgramData> SubPrograms { get; set; }
}

public class SubProgramData
{
    [JsonIgnore]
    public string ProcessName => Path.GetFileNameWithoutExtension(ProgramPath);
    public string ProgramPath { get; set; }
    public string? WorkingDirectory { get; set; }

    [JsonIgnore]
    public FileInfo ParsedPath => new FileInfo(Environment.ExpandEnvironmentVariables(ProgramPath));
    public List<string> Arguments { get; set; } = new List<string>();
    public bool KeepRunning { get; set; } = false;
    public bool UseShellExecute { get; set; } = false;
    public bool CreateNoWindow { get; set; } = false;
    public bool AlwaysRun { get; set; } = false;
    public TimeSpan? Delay { get; set; }
}

public static class Extensions
{
    public static bool WithTimeout(this Action task, TimeSpan timeout)
    {
        var t = Task.Run(task);
        if (t.Wait(timeout))
            return false;
        return true;
    }
}
