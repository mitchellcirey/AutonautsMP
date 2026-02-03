using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
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

Console.Title = MOD_NAME;
PrintBanner();

try
{
    // Find game
    Console.WriteLine("[1/5] Locating Autonauts...");
    string? gamePath = FindGamePath();
    
    if (gamePath == null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Could not find Autonauts automatically.");
        Console.ResetColor();
        Console.WriteLine("Enter the path to your Autonauts folder:");
        Console.Write("> ");
        gamePath = Console.ReadLine()?.Trim().Trim('"');
    }
    
    if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Autonauts.exe")))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ERROR: Autonauts.exe not found.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  Found: {gamePath}");
    Console.ResetColor();

    // Check/Install BepInEx
    Console.WriteLine("\n[2/5] Checking BepInEx...");
    string bepinexCore = Path.Combine(gamePath, "BepInEx", "core");
    string winhttp = Path.Combine(gamePath, "winhttp.dll");
    
    if (Directory.Exists(bepinexCore) && File.Exists(winhttp))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  BepInEx already installed");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("  Downloading BepInEx 5.4.22...");
        await InstallBepInEx(gamePath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  BepInEx installed!");
        Console.ResetColor();
    }

    // Check installed version
    Console.WriteLine("\n[3/5] Checking installed mod...");
    string modDir = Path.Combine(gamePath, "BepInEx", "plugins", "AutonautsMP");
    string modPath = Path.Combine(modDir, MOD_DLL);
    string versionPath = Path.Combine(modDir, "version.txt");
    string? installedVersion = null;
    
    if (File.Exists(modPath))
    {
        installedVersion = GetInstalledVersion(versionPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Installed version: {installedVersion ?? "unknown"}");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Mod not installed yet");
        Console.ResetColor();
    }

    // Check for local files (bundled with installer) or GitHub updates
    Console.WriteLine("\n[4/5] Checking for updates...");
    
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
            Console.WriteLine($"  Local version: {localVersion}");
            Console.WriteLine($"  Remote version: {remoteVersion}");
        }
        else
        {
            sourceVersion = remoteVersion;
            Console.WriteLine($"  Local version: {localVersion}");
            Console.WriteLine($"  Remote version: {remoteVersion} (newer)");
        }
    }
    else if (localVersion != null)
    {
        sourceVersion = localVersion;
        useLocal = true;
        Console.WriteLine($"  Local version: {localVersion}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Could not check GitHub for updates");
        Console.ResetColor();
    }
    else if (remoteVersion != null)
    {
        sourceVersion = remoteVersion;
        Console.WriteLine($"  Remote version: {remoteVersion}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ERROR: No mod files found locally or on GitHub.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }

    // Check if update needed
    bool needsUpdate = installedVersion == null || CompareVersions(sourceVersion, installedVersion) > 0;
    
    if (!needsUpdate)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║           YOU HAVE THE LATEST VERSION!               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        
        AskToLaunchGame(gamePath);
        WaitAndExit(0);
        return;
    }

    // Show what's happening
    Console.WriteLine("\n[5/5] Installing...");
    Console.ForegroundColor = ConsoleColor.Cyan;
    if (installedVersion == null)
        Console.WriteLine($"  Installing v{sourceVersion}");
    else
        Console.WriteLine($"  Updating: v{installedVersion} → v{sourceVersion}");
    Console.ResetColor();
    
    if (!useLocal && !string.IsNullOrWhiteSpace(releaseNotes))
    {
        Console.WriteLine("\n  What's new:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var lines = releaseNotes.Split('\n').Take(5);
        foreach (var line in lines)
            Console.WriteLine($"    {line.Trim()}");
        if (releaseNotes.Split('\n').Length > 5)
            Console.WriteLine("    ...");
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
        Console.WriteLine("  Cache cleared");
    }

    // Done
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("╔══════════════════════════════════════════════════════╗");
    Console.WriteLine("║        INSTALLATION COMPLETED SUCCESSFULLY!          ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Press F10 or click 'MP' button in-game to open multiplayer!");
    
    AskToCreateShortcut();
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
    Console.WriteLine("  Installer & Updater");
    Console.WriteLine("  Installs BepInEx + AutonautsMP, checks for updates\n");
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

void AskToCreateShortcut()
{
    // Check if shortcut already exists
    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    string shortcutPath = Path.Combine(desktopPath, "AutonautsMP.lnk");
    
    if (File.Exists(shortcutPath))
        return; // Already have shortcut
    
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Create desktop shortcut for easy updates? [Y/N]: ");
    Console.ResetColor();
    
    while (true)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Y)
        {
            Console.WriteLine("Y");
            CreateDesktopShortcut();
            break;
        }
        else if (key == ConsoleKey.N)
        {
            Console.WriteLine("N");
            break;
        }
    }
}

void CreateDesktopShortcut()
{
    try
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string shortcutPath = Path.Combine(desktopPath, "AutonautsMP.lnk");
        string targetPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        
        if (string.IsNullOrEmpty(targetPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Could not determine executable path");
            Console.ResetColor();
            return;
        }
        
        // Use PowerShell to create shortcut (works without COM interop)
        string psScript = $@"
$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
$shortcut.WorkingDirectory = '{Path.GetDirectoryName(targetPath)?.Replace("'", "''")}'
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Desktop shortcut created!");
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
            LaunchGame(gamePath);
            break;
        }
        else if (key == ConsoleKey.N)
        {
            Console.WriteLine("N");
            break;
        }
    }
}

void LaunchGame(string gamePath)
{
    try
    {
        string exePath = Path.Combine(gamePath, "Autonauts.exe");
        if (File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gamePath,
                UseShellExecute = true
            });
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Game launched!");
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to launch: {ex.Message}");
        Console.ResetColor();
    }
}

void WaitAndExit(int code)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
    Environment.Exit(code);
}
