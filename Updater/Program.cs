using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// ============ CONFIGURATION ============
// TODO: Update these with your actual GitHub repository details
const string GITHUB_OWNER = "mitchellcirey";
const string GITHUB_REPO = "AutonautsMP";
// =======================================

const string MOD_NAME = "AutonautsMP";
const string MOD_DLL = "AutonautsMP.dll";

Console.Title = $"{MOD_NAME} Updater";
PrintBanner();

try
{
    // Find installed mod
    Console.WriteLine("[1/4] Locating installed mod...");
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
        Console.WriteLine("ERROR: Autonauts not found.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    string modDir = Path.Combine(gamePath, "BepInEx", "plugins", "AutonautsMP");
    string modPath = Path.Combine(modDir, MOD_DLL);
    string versionPath = Path.Combine(modDir, "version.txt");
    string? installedVersion = null;
    
    if (File.Exists(modPath))
    {
        installedVersion = GetInstalledVersion(versionPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Found: {modPath}");
        Console.WriteLine($"  Installed version: {installedVersion ?? "unknown"}");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Mod not installed yet");
        Console.ResetColor();
    }

    // Check GitHub for latest release
    Console.WriteLine("\n[2/4] Checking for updates...");
    var (latestVersion, downloadUrl, releaseNotes) = await GetLatestRelease();
    
    if (latestVersion == null || downloadUrl == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ERROR: Could not fetch release information from GitHub.");
        Console.WriteLine("Make sure the repository has at least one release with a ZIP asset.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    Console.WriteLine($"  Latest version: {latestVersion}");
    
    // Compare versions
    Console.WriteLine("\n[3/4] Comparing versions...");
    bool needsUpdate = installedVersion == null || CompareVersions(latestVersion, installedVersion) > 0;
    
    if (!needsUpdate)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║           YOU HAVE THE LATEST VERSION!               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        WaitAndExit(0);
        return;
    }
    
    // Show what's new
    Console.ForegroundColor = ConsoleColor.Cyan;
    if (installedVersion == null)
        Console.WriteLine($"  New installation: v{latestVersion}");
    else
        Console.WriteLine($"  Update available: v{installedVersion} → v{latestVersion}");
    Console.ResetColor();
    
    if (!string.IsNullOrWhiteSpace(releaseNotes))
    {
        Console.WriteLine("\n  What's new:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        // Show first few lines of release notes
        var lines = releaseNotes.Split('\n').Take(5);
        foreach (var line in lines)
        {
            Console.WriteLine($"    {line.Trim()}");
        }
        if (releaseNotes.Split('\n').Length > 5)
            Console.WriteLine("    ...");
        Console.ResetColor();
    }
    
    // Ask for confirmation
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Download and install update? [Y/N]: ");
    Console.ResetColor();
    
    while (true)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Y)
        {
            Console.WriteLine("Y");
            break;
        }
        else if (key == ConsoleKey.N)
        {
            Console.WriteLine("N");
            Console.WriteLine("\nUpdate cancelled.");
            WaitAndExit(0);
            return;
        }
    }

    // Close game if running
    Console.WriteLine("\n[4/4] Installing update...");
    CloseGameIfRunning();
    
    // Download and install
    Console.WriteLine("  Downloading...");
    string tempZip = Path.Combine(Path.GetTempPath(), $"{MOD_NAME}_update.zip");
    
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(MOD_NAME, latestVersion.ToString()));
        
        var response = await http.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(tempZip, bytes);
        
        Console.WriteLine("  Extracting...");
        string tempExtract = Path.Combine(Path.GetTempPath(), $"{MOD_NAME}_update");
        if (Directory.Exists(tempExtract))
            Directory.Delete(tempExtract, true);
        
        ZipFile.ExtractToDirectory(tempZip, tempExtract);
        
        // Find and copy mod files
        Directory.CreateDirectory(modDir);
        
        // Look for mod files in the extracted archive
        var dllFiles = Directory.GetFiles(tempExtract, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
        {
            string fileName = Path.GetFileName(dll);
            string destPath = Path.Combine(modDir, fileName);
            Console.WriteLine($"  Installing: {fileName}");
            File.Copy(dll, destPath, overwrite: true);
        }
        
        // Copy version.txt if present
        var versionFiles = Directory.GetFiles(tempExtract, "version.txt", SearchOption.AllDirectories);
        foreach (var vf in versionFiles)
        {
            string destPath = Path.Combine(modDir, "version.txt");
            File.Copy(vf, destPath, overwrite: true);
        }
        
        // Clear BepInEx cache
        string cachePath = Path.Combine(gamePath, "BepInEx", "cache");
        if (Directory.Exists(cachePath))
        {
            Directory.Delete(cachePath, true);
            Console.WriteLine("  Cache cleared");
        }
        
        // Cleanup
        Directory.Delete(tempExtract, true);
    }
    finally
    {
        if (File.Exists(tempZip))
            File.Delete(tempZip);
    }

    // Done
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("╔══════════════════════════════════════════════════════╗");
    Console.WriteLine("║            UPDATE INSTALLED SUCCESSFULLY!            ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
    
    // Ask if user wants to launch the game
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
catch (HttpRequestException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Network error - {ex.Message}");
    Console.WriteLine("Check your internet connection and try again.");
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
    Console.WriteLine("  Auto-Updater v1.0");
    Console.WriteLine("  Checks GitHub for the latest release\n");
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

int CompareVersions(string v1, string v2)
{
    // Compare semantic versions like "1.0.0" vs "1.2.3"
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
        
        // Get version from tag name (strip 'v' prefix if present)
        string tagName = root.GetProperty("tag_name").GetString() ?? "";
        string? version = tagName.TrimStart('v', 'V');
        
        // Try to extract version from tag like "release-1.2.3" if needed
        if (string.IsNullOrEmpty(version) || !char.IsDigit(version[0]))
        {
            var match = Regex.Match(tagName, @"(\d+\.\d+\.\d+)");
            version = match.Success ? match.Groups[1].Value : null;
        }
        
        // Get release notes
        string? releaseNotes = root.GetProperty("body").GetString();
        
        // Find ZIP asset
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
        
        // Fallback to zipball if no ZIP asset
        if (downloadUrl == null)
        {
            downloadUrl = root.GetProperty("zipball_url").GetString();
        }
        
        return (version, downloadUrl, releaseNotes);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  No releases found on GitHub yet.");
        Console.ResetColor();
        return (null, null, null);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Failed to check GitHub: {ex.Message}");
        Console.ResetColor();
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
    
    // Try Steam registry
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
        Console.WriteLine("  Autonauts is running - closing...");
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
