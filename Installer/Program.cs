using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Win32;

const string BEPINEX_URL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";
const string MOD_NAME = "AutonautsMP";

Console.Title = $"{MOD_NAME} Installer";
PrintBanner();

try
{
    // Find game
    Console.WriteLine("[1/4] Locating Autonauts...");
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
        Console.WriteLine("ERROR: Autonauts.exe not found. Installation cancelled.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  Found: {gamePath}");
    Console.ResetColor();

    // Check/Install BepInEx
    Console.WriteLine("\n[2/4] Checking BepInEx...");
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

    // Install mod - directly into plugins folder (NOT a subfolder)
    Console.WriteLine("\n[3/4] Installing AutonautsMP...");
    string pluginsDir = Path.Combine(gamePath, "BepInEx", "plugins");
    Directory.CreateDirectory(pluginsDir);
    
    // Find mod DLL
    string? modDll = FindModDll();
    if (modDll == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ERROR: AutonautsMP.dll not found. Build the project first.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    
    // Copy directly to plugins folder (no subfolder - avoids some scanning issues)
    string destPath = Path.Combine(pluginsDir, "AutonautsMP.dll");
    File.Copy(modDll, destPath, overwrite: true);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  Installed to: {destPath}");
    Console.ResetColor();

    // Clear cache to avoid stale data
    Console.WriteLine("\n[4/4] Clearing BepInEx cache...");
    string cachePath = Path.Combine(gamePath, "BepInEx", "cache");
    if (Directory.Exists(cachePath))
    {
        Directory.Delete(cachePath, true);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Cache cleared");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("  No cache to clear");
    }

    // Done
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("╔══════════════════════════════════════════════════════╗");
    Console.WriteLine("║        INSTALLATION COMPLETED SUCCESSFULLY!          ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Launch Autonauts and press F10 or click 'MP' button!");
    Console.WriteLine();
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
    Console.WriteLine("  One-Click Installer v1.0");
    Console.WriteLine("  BepInEx 5.4.22 + AutonautsMP\n");
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

string? FindModDll()
{
    // Check same folder as installer
    string here = AppContext.BaseDirectory;
    string samePath = Path.Combine(here, "AutonautsMP.dll");
    if (File.Exists(samePath)) return samePath;
    
    // Check parent directories for build output
    string? dir = here;
    for (int i = 0; i < 6; i++)
    {
        dir = Path.GetDirectoryName(dir);
        if (dir == null) break;
        
        string[] checks = {
            Path.Combine(dir, "bin", "Release", "AutonautsMP.dll"),
            Path.Combine(dir, "bin", "Debug", "AutonautsMP.dll"),
            Path.Combine(dir, "AutonautsMP.dll"),
        };
        foreach (var p in checks)
            if (File.Exists(p)) return p;
    }
    return null;
}

void WaitAndExit(int code)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(true);
    Environment.Exit(code);
}
