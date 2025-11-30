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
    config = JsonSerializer.Deserialize<MainConfig>(json) ?? throw new ArgumentNullException("config");

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
        Log($"[{cnt}] Process: {processData.ProcessName} (Enabled: {processData.Enabled})");
        Log($"[{cnt}] Grace Period: {processData.GracePeriod}");
        var subcnt = 1;
        foreach (var subProgramData in processData.SubPrograms)
        {
            Log($"[{cnt}.{subcnt}] Process: {subProgramData.ProcessName} (Enabled: {subProgramData.Enabled})");
            Log(
                $"[{cnt}.{subcnt}] Program Path: \"{subProgramData.ProgramPath}\" ({(subProgramData.ParsedPath.Exists ? "valid" : "invalid")})"
            );
            Log($"[{cnt}.{subcnt}] Arguments: {string.Join(" ", subProgramData.Arguments)}");
            Log($"[{cnt}.{subcnt}] Keep Running: {subProgramData.KeepRunning}");
            Log($"[{cnt}.{subcnt}] Always Run: {subProgramData.AlwaysRun}");
            Log($"[{cnt}.{subcnt}] Delay: {subProgramData.Delay}");
            if (subProgramData.EnvironmentVariables != null && subProgramData.EnvironmentVariables.Count > 0)
            {
                Log($"[{cnt}.{subcnt}] Environment Variables: {string.Join(", ", subProgramData.EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
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
            if (!processData.Enabled)
                continue;
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
                        if (!subProgramData.Enabled)
                            continue;
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
                                    ?? subProgramData.ParsedPath.Directory?.FullName
                                    ?? null;
                                StartProcessFromFile(
                                    path: subProgramData.ParsedPath,
                                    args: subProgramData.Arguments,
                                    workDir: workDir,
                                    noWindow: subProgramData.CreateNoWindow,
                                    shellExecute: subProgramData.UseShellExecute,
                                    environmentVariables: subProgramData.EnvironmentVariables
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
                        if (!subProgramData.Enabled || subProgramData.KeepRunning)
                            continue;
                        Process[] processes;
                        try
                        {
                            processes = Process.GetProcessesByName(subProgramData.ProcessName);
                        }
                        catch
                        {
                            processes = Array.Empty<Process>();
                        }
                        
                        foreach (var proc in processes)
                        {
                            new Thread(() =>
                            {
                                try
                                {
                                    // Check if process has already exited
                                    try
                                    {
                                        if (proc.HasExited)
                                        {
                                            proc.Dispose();
                                            return;
                                        }
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // Process might have exited between GetProcessesByName and here
                                        try
                                        {
                                            proc.Dispose();
                                        }
                                        catch { }
                                        return;
                                    }
                                    
                                    Log($"Closing {subProgramData.ProcessName}");
                                    try
                                    {
                                        bool hasMainWindow = false;
                                        try
                                        {
                                            hasMainWindow = proc.MainWindowHandle != IntPtr.Zero;
                                        }
                                        catch { }
                                        
                                        if (hasMainWindow && proc.CloseMainWindow())
                                        {
                                            if (!proc.WaitForExit(500))
                                            {
                                                if (!proc.HasExited)
                                                {
                                                    Log(
                                                        $"{subProgramData.ProcessName} did not close main window in time, closing process!"
                                                    );
                                                    try
                                                    {
                                                        if (!proc.HasExited)
                                                        {
                                                            proc.Close();
                                                            if (!proc.WaitForExit(250))
                                                            {
                                                                if (!proc.HasExited)
                                                                {
                                                                    Log(
                                                                        $"{subProgramData.ProcessName} did not close in time, killing process!"
                                                                    );
                                                                    try
                                                                    {
                                                                        if (!proc.HasExited)
                                                                        {
                                                                            proc.Kill();
                                                                            if (!proc.WaitForExit(250))
                                                                            {
                                                                                if (!proc.HasExited)
                                                                                {
                                                                                    Log(
                                                                                        $"{subProgramData.ProcessName} did not die in time, ignoring!"
                                                                                    );
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                    catch (Exception killEx)
                                                                    {
                                                                        Log($"Error killing {subProgramData.ProcessName}: {killEx.Message}");
                                                                        StartProcess(
                                                                            "taskkill",
                                                                            new() { "/f", "/im", subProgramData.ParsedPath.Name },
                                                                            noWindow: true
                                                                        );
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception closeEx)
                                                    {
                                                        Log($"Error closing {subProgramData.ProcessName}: {closeEx.Message}");
                                                        try
                                                        {
                                                            if (!proc.HasExited)
                                                            {
                                                                proc.Kill();
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Log($"Closed {subProgramData.ProcessName}");
                                            }
                                        }
                                        else
                                        {
                                            // No main window or CloseMainWindow returned false, try to kill directly
                                            if (!proc.HasExited)
                                            {
                                                Log(
                                                    $"{subProgramData.ProcessName} has no main window, killing process directly"
                                                );
                                                try
                                                {
                                                    proc.Kill();
                                                    if (proc.WaitForExit(250))
                                                    {
                                                        Log($"Killed {subProgramData.ProcessName}");
                                                    }
                                                    else if (!proc.HasExited)
                                                    {
                                                        Log($"{subProgramData.ProcessName} did not die in time, ignoring!");
                                                    }
                                                }
                                                catch (Exception killEx)
                                                {
                                                    Log($"Error killing {subProgramData.ProcessName}: {killEx.Message}");
                                                    StartProcess(
                                                        "taskkill",
                                                        new() { "/f", "/im", subProgramData.ParsedPath.Name },
                                                        noWindow: true
                                                    );
                                                }
                                            }
                                        }
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // Process has exited
                                        Log($"Process {subProgramData.ProcessName} already exited");
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            proc.Dispose();
                                        }
                                        catch { }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"Exception closing {subProgramData.ProcessName}: {ex.Message}");
                                    try
                                    {
                                        StartProcess(
                                            "taskkill",
                                            new() { "/f", "/im", subProgramData.ParsedPath.Name },
                                            noWindow: true
                                        );
                                    }
                                    catch { }
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
    bool shellExecute = false,
    Dictionary<string, string>? environmentVariables = null
)
{
    var extension = path.Extension.ToLowerInvariant();
    string executablePath = path.FullName;
    List<string> executableArgs = new List<string>(args);
    
    // Handle PowerShell scripts
    if (extension == ".ps1")
    {
        // Try pwsh.exe first (PowerShell 7+), then fall back to powershell.exe
        var pwshPath = "";
        
        // Check common PowerShell 7 installation paths
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var possiblePaths = new[]
        {
            Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
            Path.Combine(programFiles, "PowerShell", "7-preview", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "pwsh.exe"),
            Environment.GetEnvironmentVariable("POWERSHELL7_PATH") ?? "",
        };
        
        foreach (var possiblePath in possiblePaths)
        {
            if (string.IsNullOrEmpty(possiblePath))
                continue;
                
            if (File.Exists(possiblePath))
            {
                pwshPath = possiblePath;
                break;
            }
        }
        
        // If not found in common paths, try "pwsh.exe" (will search PATH)
        if (string.IsNullOrEmpty(pwshPath))
        {
            pwshPath = "pwsh.exe";
        }
        
        executablePath = pwshPath;
        executableArgs.Insert(0, "-NoProfile");
        executableArgs.Insert(1, "-ExecutionPolicy");
        executableArgs.Insert(2, "Bypass");
        executableArgs.Insert(3, "-File");
        executableArgs.Insert(4, path.FullName);
        
        // If pwsh.exe fails, fall back to Windows PowerShell will happen naturally via exception handling
        StartProcess(executablePath, executableArgs, workDir, noWindow, false, environmentVariables);
        return;
    }
    
    // Handle batch files - ensure they use shell execute or cmd.exe
    if (extension == ".bat" || extension == ".cmd")
    {
        // If shellExecute is not set, run via cmd.exe
        if (!shellExecute)
        {
            executablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (!File.Exists(executablePath))
            {
                executablePath = "cmd.exe";
            }
            executableArgs.Insert(0, "/c");
            executableArgs.Insert(1, path.FullName);
            StartProcess(executablePath, executableArgs, workDir, noWindow, false, environmentVariables);
            return;
        }
    }
    
    StartProcess(executablePath, executableArgs, workDir, noWindow, shellExecute, environmentVariables);
}

void StartProcess(
    string path,
    List<string> args,
    string? workDir = null,
    bool noWindow = false,
    bool shellExecute = false,
    Dictionary<string, string>? environmentVariables = null
)
{
    var proc = new ProcessStartInfo(path, string.Join(" ", args))
    {
        WorkingDirectory = workDir,
        UseShellExecute = shellExecute,
        CreateNoWindow = noWindow,
    };
    
    if (environmentVariables != null)
    {
        foreach (var envVar in environmentVariables)
        {
            proc.EnvironmentVariables[envVar.Key] = envVar.Value;
        }
    }
    
    Log($"Running \"{path}\" {proc.Arguments}");
    Process.Start(proc);
}

bool IsProcessRunning(string name)
{
    try
    {
        if (config.CaseInsensitive)
        {
            var processes = Process.GetProcesses();
            try
            {
                return processes.Any(p =>
                {
                    try
                    {
                        return string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            finally
            {
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Dispose();
                    }
                    catch { }
                }
            }
        }
        else
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Dispose();
                    }
                    catch { }
                }
            }
        }
    }
    catch
    {
        return false;
    }
}

public class MainConfig
{
    public required List<ProcessConfig> Processes { get; set; }
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool LogToConsole { get; set; } = false;
    public bool LogToFile { get; set; } = false;
    public bool CaseInsensitive { get; set; } = false;
}

public class ProcessConfig
{
    public required string ProcessName { get; set; }
    public TimeSpan? GracePeriod { get; set; } = TimeSpan.FromSeconds(5);
    public required List<SubProgramData> SubPrograms { get; set; }
    public bool Enabled { get; set; } = true;
}

public class SubProgramData
{
    [JsonIgnore]
    public string ProcessName => Path.GetFileNameWithoutExtension(ProgramPath);
    public required string ProgramPath { get; set; }
    public string? WorkingDirectory { get; set; }

    [JsonIgnore]
    public FileInfo ParsedPath => new FileInfo(Environment.ExpandEnvironmentVariables(ProgramPath));
    public List<string> Arguments { get; set; } = new List<string>();
    public bool KeepRunning { get; set; } = false;
    public bool UseShellExecute { get; set; } = false;
    public bool CreateNoWindow { get; set; } = false;
    public bool AlwaysRun { get; set; } = false;
    public TimeSpan? Delay { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
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
