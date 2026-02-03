using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// ============ CONFIGURATION ============
const string GITHUB_OWNER = "mitchellcirey";
const string GITHUB_REPO = "AutonautsMP";
// =======================================

const string MOD_NAME = "AutonautsMP";
const string MOD_DLL = "AutonautsMP.dll";
const string BEPINEX_URL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";

// Setup console window
SetupConsoleWindow();
Console.Title = MOD_NAME;
PrintBanner();

try
{
    // Step 1: Find game
    PrintStep(1, 5, "Locating Autonauts...");
    string? gamePath = FindGamePath();
    
    if (gamePath == null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("    Could not find Autonauts automatically.");
        Console.ResetColor();
        Console.WriteLine("    Enter the path to your Autonauts folder:");
        Console.Write("    > ");
        gamePath = Console.ReadLine()?.Trim().Trim('"');
    }
    
    if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Autonauts.exe")))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("    ERROR: Autonauts.exe not found.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"    Found: {gamePath}");
    Console.ResetColor();
    Console.WriteLine();

    // Step 2: Check/Install BepInEx
    PrintStep(2, 5, "Checking BepInEx...");
    string bepinexCore = Path.Combine(gamePath, "BepInEx", "core");
    string winhttp = Path.Combine(gamePath, "winhttp.dll");
    
    if (Directory.Exists(bepinexCore) && File.Exists(winhttp))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("    Already installed");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("    Downloading BepInEx 5.4.22...");
        await InstallBepInEx(gamePath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("    Installed!");
        Console.ResetColor();
    }
    Console.WriteLine();

    // Step 3: Check installed version
    PrintStep(3, 5, "Checking installed mod...");
    string modDir = Path.Combine(gamePath, "BepInEx", "plugins", "AutonautsMP");
    string modPath = Path.Combine(modDir, MOD_DLL);
    string versionPath = Path.Combine(modDir, "version.txt");
    string? installedVersion = null;
    
    if (File.Exists(modPath))
    {
        installedVersion = GetInstalledVersion(versionPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    Installed: v{installedVersion ?? "unknown"}");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("    Not installed yet");
        Console.ResetColor();
    }
    Console.WriteLine();

    // Step 4: Check for local files (bundled with installer) or GitHub updates
    PrintStep(4, 5, "Checking for updates...");
    
    string? localVersion = GetLocalVersion();
    var (remoteVersion, downloadUrl, releaseNotes) = await GetLatestRelease();
    
    // Determine best source
    string? sourceVersion = null;
    bool useLocal = false;
    
    if (localVersion != null && remoteVersion != null)
    {
        // Both available - use whichever is newer
        if (CompareVersions(localVersion, remoteVersion) >= 0)
        {
            sourceVersion = localVersion;
            useLocal = true;
        }
        else
        {
            sourceVersion = remoteVersion;
        }
        Console.WriteLine($"    Local: v{localVersion}  |  Remote: v{remoteVersion}");
    }
    else if (localVersion != null)
    {
        sourceVersion = localVersion;
        useLocal = true;
        Console.WriteLine($"    Local: v{localVersion}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("    (Could not reach GitHub)");
        Console.ResetColor();
    }
    else if (remoteVersion != null)
    {
        sourceVersion = remoteVersion;
        Console.WriteLine($"    Latest: v{remoteVersion}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("    ERROR: No mod files found locally or on GitHub.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    Console.WriteLine();

    // Check if update needed
    bool needsUpdate = installedVersion == null || CompareVersions(sourceVersion, installedVersion) > 0;
    
    if (!needsUpdate)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  You have the latest version!");
        Console.ResetColor();
        Console.WriteLine();
        
        CreateOrUpdateShortcut(modDir);
        AskToLaunchGame(gamePath);
        WaitAndExit(0);
        return;
    }

    // Step 5: Installing
    PrintStep(5, 5, installedVersion == null 
        ? $"Installing v{sourceVersion}..." 
        : $"Updating v{installedVersion} -> v{sourceVersion}...");
    
    if (!useLocal && !string.IsNullOrWhiteSpace(releaseNotes))
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    What's new:");
        var lines = releaseNotes.Split('\n').Take(3);
        foreach (var line in lines)
            Console.WriteLine($"      {line.Trim()}");
        Console.ResetColor();
    }

    // Close game if running
    CloseGameIfRunning();
    
    // Install from local or download
    Directory.CreateDirectory(modDir);
    
    if (useLocal)
    {
        // Install from local files
        InstallFromLocal(modDir);
    }
    else
    {
        // Download and install from GitHub
        await InstallFromGitHub(downloadUrl!, modDir);
    }

    // Clear BepInEx cache
    string cachePath = Path.Combine(gamePath, "BepInEx", "cache");
    if (Directory.Exists(cachePath))
    {
        Directory.Delete(cachePath, true);
        Console.WriteLine("    Cache cleared");
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("    Done!");
    Console.ResetColor();
    Console.WriteLine();
    
    // Create shortcut before showing success
    CreateOrUpdateShortcut(modDir);
    Console.WriteLine();

    // Success message
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ╔═══════════════════════════════════════╗");
    Console.WriteLine("  ║                                       ║");
    Console.WriteLine("  ║       INSTALLATION COMPLETE!          ║");
    Console.WriteLine("  ║                                       ║");
    Console.WriteLine("  ╚═══════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  Press F10 or click 'AMP' button in-game to open multiplayer.");
    Console.WriteLine();
    
    AskToLaunchGame(gamePath);
}
catch (HttpRequestException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Network error - {ex.Message}");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.ResetColor();
}

WaitAndExit(0);

// ============ Helper Methods ============

// Windows API for disabling resize
[DllImport("kernel32.dll", ExactSpelling = true)]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

[DllImport("user32.dll")]
static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

const uint SC_SIZE = 0xF000;
const uint SC_MAXIMIZE = 0xF030;
const uint MF_BYCOMMAND = 0x00000000;

void SetupConsoleWindow()
{
    try
    {
        // Set fixed console size
        Console.SetWindowSize(80, 30);
        Console.SetBufferSize(80, 300);
        
        // Disable resize and maximize
        IntPtr handle = GetConsoleWindow();
        IntPtr sysMenu = GetSystemMenu(handle, false);
        if (sysMenu != IntPtr.Zero)
        {
            DeleteMenu(sysMenu, SC_SIZE, MF_BYCOMMAND);
            DeleteMenu(sysMenu, SC_MAXIMIZE, MF_BYCOMMAND);
        }
    }
    catch { } // Ignore errors on non-Windows or if console APIs fail
}

void PrintStep(int step, int total, string task)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write($"[{step}/{total}] ");
    Console.ResetColor();
    Console.WriteLine(task);
}

void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"
     _         _                         _       __  __ ____  
    / \  _   _| |_ ___  _ __   __ _ _   _| |_ ___|  \/  |  _ \ 
   / _ \| | | | __/ _ \| '_ \ / _` | | | | __/ __| |\/| | |_) |
  / ___ \ |_| | || (_) | | | | (_| | |_| | |_\__ \ |  | |  __/ 
 /_/   \_\__,_|\__\___/|_| |_|\__,_|\__,_|\__|___/_|  |_|_|    
");
    Console.ResetColor();
    Console.WriteLine("  Authors: mitchellcirey + DMarie68");
    Console.WriteLine("  Installer & Updater for AutonautsMP & BepInEx\n");
}

string? GetInstalledVersion(string versionFilePath)
{
    try
    {
        if (File.Exists(versionFilePath))
            return File.ReadAllText(versionFilePath).Trim();
    }
    catch { }
    return null;
}

string? GetLocalVersion()
{
    // Check for version.txt in same folder as this exe
    string here = AppContext.BaseDirectory;
    string versionFile = Path.Combine(here, "version.txt");
    string dllFile = Path.Combine(here, MOD_DLL);
    
    if (File.Exists(versionFile) && File.Exists(dllFile))
    {
        try { return File.ReadAllText(versionFile).Trim(); }
        catch { }
    }
    return null;
}

void InstallFromLocal(string modDir)
{
    string here = AppContext.BaseDirectory;
    
    // Copy DLLs
    foreach (var dll in Directory.GetFiles(here, "*.dll"))
    {
        string fileName = Path.GetFileName(dll);
        // Skip system DLLs that might be in the folder
        if (fileName.StartsWith("System.") || fileName.StartsWith("Microsoft."))
            continue;
            
        string destPath = Path.Combine(modDir, fileName);
        Console.WriteLine($"  Installing: {fileName}");
        File.Copy(dll, destPath, overwrite: true);
    }
    
    // Copy exe (the installer itself)
    foreach (var exe in Directory.GetFiles(here, "*.exe"))
    {
        string fileName = Path.GetFileName(exe);
        string destPath = Path.Combine(modDir, fileName);
        Console.WriteLine($"  Installing: {fileName}");
        File.Copy(exe, destPath, overwrite: true);
    }
    
    // Copy version.txt
    string versionSource = Path.Combine(here, "version.txt");
    if (File.Exists(versionSource))
    {
        File.Copy(versionSource, Path.Combine(modDir, "version.txt"), overwrite: true);
    }
}

async Task InstallFromGitHub(string downloadUrl, string modDir)
{
    Console.WriteLine("  Downloading...");
    string tempZip = Path.Combine(Path.GetTempPath(), $"{MOD_NAME}_update.zip");
    
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(MOD_NAME, "1.0"));
        
        var response = await http.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(tempZip, bytes);
        
        Console.WriteLine("  Extracting...");
        string tempExtract = Path.Combine(Path.GetTempPath(), $"{MOD_NAME}_update");
        if (Directory.Exists(tempExtract))
            Directory.Delete(tempExtract, true);
        
        ZipFile.ExtractToDirectory(tempZip, tempExtract);
        
        // Copy DLLs
        var dllFiles = Directory.GetFiles(tempExtract, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
        {
            string fileName = Path.GetFileName(dll);
            string destPath = Path.Combine(modDir, fileName);
            Console.WriteLine($"  Installing: {fileName}");
            File.Copy(dll, destPath, overwrite: true);
        }
        
        // Copy exe (the installer itself)
        var exeFiles = Directory.GetFiles(tempExtract, "*.exe", SearchOption.AllDirectories);
        foreach (var exe in exeFiles)
        {
            string fileName = Path.GetFileName(exe);
            string destPath = Path.Combine(modDir, fileName);
            Console.WriteLine($"  Installing: {fileName}");
            File.Copy(exe, destPath, overwrite: true);
        }
        
        // Copy version.txt
        var versionFiles = Directory.GetFiles(tempExtract, "version.txt", SearchOption.AllDirectories);
        foreach (var vf in versionFiles)
        {
            File.Copy(vf, Path.Combine(modDir, "version.txt"), overwrite: true);
        }
        
        Directory.Delete(tempExtract, true);
    }
    finally
    {
        if (File.Exists(tempZip))
            File.Delete(tempZip);
    }
}

async Task InstallBepInEx(string gamePath)
{
    string tempZip = Path.Combine(Path.GetTempPath(), "BepInEx_5.4.22.zip");
    try
    {
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(BEPINEX_URL);
        await File.WriteAllBytesAsync(tempZip, bytes);
        ZipFile.ExtractToDirectory(tempZip, gamePath, overwriteFiles: true);
    }
    finally
    {
        if (File.Exists(tempZip)) File.Delete(tempZip);
    }
}

int CompareVersions(string v1, string v2)
{
    var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
    var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
    
    for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
    {
        int p1 = i < parts1.Length ? parts1[i] : 0;
        int p2 = i < parts2.Length ? parts2[i] : 0;
        if (p1 != p2) return p1.CompareTo(p2);
    }
    return 0;
}

async Task<(string? version, string? downloadUrl, string? releaseNotes)> GetLatestRelease()
{
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(MOD_NAME, "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        
        string apiUrl = $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";
        var response = await http.GetStringAsync(apiUrl);
        
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        
        string tagName = root.GetProperty("tag_name").GetString() ?? "";
        string? version = tagName.TrimStart('v', 'V');
        
        if (string.IsNullOrEmpty(version) || !char.IsDigit(version[0]))
        {
            var match = Regex.Match(tagName, @"(\d+\.\d+\.\d+)");
            version = match.Success ? match.Groups[1].Value : null;
        }
        
        string? releaseNotes = root.GetProperty("body").GetString();
        
        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string? name = asset.GetProperty("name").GetString();
                if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }
        
        if (downloadUrl == null)
            downloadUrl = root.GetProperty("zipball_url").GetString();
        
        return (version, downloadUrl, releaseNotes);
    }
    catch
    {
        return (null, null, null);
    }
}

string? FindGamePath()
{
    string[] defaultPaths = {
        @"C:\Program Files (x86)\Steam\steamapps\common\Autonauts",
        @"C:\Program Files\Steam\steamapps\common\Autonauts",
        @"D:\Steam\steamapps\common\Autonauts",
        @"D:\SteamLibrary\steamapps\common\Autonauts",
        @"E:\SteamLibrary\steamapps\common\Autonauts",
    };
    
    foreach (var p in defaultPaths)
        if (Directory.Exists(p) && File.Exists(Path.Combine(p, "Autonauts.exe")))
            return p;
    
    try
    {
        string? steamPath = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath", null) as string;
        
        if (steamPath != null)
        {
            string mainLib = Path.Combine(steamPath, "steamapps", "common", "Autonauts");
            if (Directory.Exists(mainLib)) return mainLib;
            
            string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), @"""path""\s+""([^""]+)"""))
                {
                    string lib = Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), 
                        "steamapps", "common", "Autonauts");
                    if (Directory.Exists(lib)) return lib;
                }
            }
        }
    }
    catch { }
    
    return null;
}

void CloseGameIfRunning()
{
    var processes = Process.GetProcessesByName("Autonauts");
    if (processes.Length > 0)
    {
        foreach (var p in processes) p.Dispose();
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  Autonauts is running - closing...");
        Console.ResetColor();
        
        processes = Process.GetProcessesByName("Autonauts");
        foreach (var process in processes)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(3000))
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
                process.Dispose();
            }
            catch { }
        }
        
        Thread.Sleep(1000);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Game closed");
        Console.ResetColor();
    }
}

void CreateOrUpdateShortcut(string modDir)
{
    // Always create/update the shortcut to ensure it points to the right location
    CreateDesktopShortcut(modDir);
}

void CreateDesktopShortcut(string modDir)
{
    try
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string shortcutPath = Path.Combine(desktopPath, "AutonautsMP.lnk");
        // Point to the exe in the install folder, not wherever it's currently running from
        string targetPath = Path.Combine(modDir, "AutonautsMP.exe");
        
        bool existed = File.Exists(shortcutPath);
        
        if (!File.Exists(targetPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Exe not found in install folder yet");
            Console.ResetColor();
            return;
        }
        
        // Use PowerShell to create shortcut (works without COM interop)
        string psScript = $@"
$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
$shortcut.WorkingDirectory = '{modDir.Replace("'", "''")}'
$shortcut.Description = 'AutonautsMP Installer & Updater'
$shortcut.Save()
";
        
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        using var process = Process.Start(psi);
        process?.WaitForExit(5000);
        
        if (File.Exists(shortcutPath))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(existed ? "Desktop shortcut updated." : "Desktop shortcut created.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Could not create shortcut");
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Could not create shortcut: {ex.Message}");
        Console.ResetColor();
    }
}

void AskToLaunchGame(string gamePath)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Would you like to launch Autonauts now? [Y/N]: ");
    Console.ResetColor();
    
    while (true)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Y)
        {
            Console.WriteLine("Y");
            Console.WriteLine("\nLaunching Autonauts...");
            LaunchGame();
            break;
        }
        else if (key == ConsoleKey.N)
        {
            Console.WriteLine("N");
            break;
        }
    }
    
    // Countdown to close
    CountdownAndExit();
}

void LaunchGame()
{
    try
    {
        // Launch via Steam to avoid the close/relaunch issue
        // Autonauts Steam App ID: 979120
        Process.Start(new ProcessStartInfo
        {
            FileName = "steam://rungameid/979120",
            UseShellExecute = true
        });
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Game launched!");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to launch: {ex.Message}");
        Console.ResetColor();
    }
}

void CountdownAndExit()
{
    Console.WriteLine();
    for (int i = 10; i > 0; i--)
    {
        Console.Write($"\rClosing in {i}...  ");
        Thread.Sleep(1000);
    }
    Console.WriteLine("\rClosing...        ");
    Environment.Exit(0);
}

void WaitAndExit(int code)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
    Environment.Exit(code);
}
