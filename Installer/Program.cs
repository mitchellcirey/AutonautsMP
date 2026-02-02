using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Win32;

const string BEPINEX_URL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";
const string MOD_NAME = "AutonautsMP";

Console.Title = $"{MOD_NAME} Installer";
PrintBanner();

try
{
    // Close game if running
    Console.WriteLine("[1/5] Checking if Autonauts is running...");
    CloseGameIfRunning();

    // Find game
    Console.WriteLine("\n[2/5] Locating Autonauts...");
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
    Console.WriteLine("\n[3/5] Checking BepInEx...");
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

    // Install mod into plugins/AutonautsMP subfolder
    Console.WriteLine("\n[4/5] Installing AutonautsMP...");
    string modDir = Path.Combine(gamePath, "BepInEx", "plugins", "AutonautsMP");
    
    // Create the AutonautsMP folder if it doesn't exist
    if (!Directory.Exists(modDir))
    {
        Console.WriteLine($"  Creating folder: {modDir}");
        Directory.CreateDirectory(modDir);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Folder created!");
        Console.ResetColor();
    }
    else
    {
        Console.WriteLine($"  Folder exists: {modDir}");
    }
    
    // Find mod DLL
    Console.WriteLine("  Locating mod files...");
    string? modDll = FindModDll();
    if (modDll == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ERROR: AutonautsMP.dll not found. Build the project first.");
        Console.ResetColor();
        WaitAndExit(1);
        return;
    }
    Console.WriteLine($"  Found: {modDll}");
    
    // Copy mod DLL to plugins/AutonautsMP folder
    Console.WriteLine("  Copying AutonautsMP.dll...");
    string destPath = Path.Combine(modDir, "AutonautsMP.dll");
    File.Copy(modDll, destPath, overwrite: true);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  Installed: {destPath}");
    Console.ResetColor();
    
    // Copy Telepathy.dll dependency (must be in same folder as mod DLL)
    string? sourceDirPath = Path.GetDirectoryName(modDll);
    if (sourceDirPath != null)
    {
        string telepathySource = Path.Combine(sourceDirPath, "Telepathy.dll");
        if (File.Exists(telepathySource))
        {
            Console.WriteLine("  Copying Telepathy.dll...");
            string telepathyDest = Path.Combine(modDir, "Telepathy.dll");
            File.Copy(telepathySource, telepathyDest, overwrite: true);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Installed: {telepathyDest}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Warning: Telepathy.dll not found - networking may not work");
            Console.ResetColor();
        }
    }

    // Clear cache to avoid stale data
    Console.WriteLine("\n[5/5] Clearing BepInEx cache...");
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

void CloseGameIfRunning()
{
    var processes = Process.GetProcessesByName("Autonauts");
    if (processes.Length > 0)
    {
        // Dispose initial check processes
        foreach (var p in processes) p.Dispose();
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Autonauts is running!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Please close Autonauts to continue, or press:");
        Console.WriteLine("    [Y] Force close the game");
        Console.WriteLine("    [N] Wait for you to close it manually");
        Console.Write("\n  > ");
        
        while (true)
        {
            // Check if key is available
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Y)
                {
                    Console.WriteLine("Y");
                    Console.WriteLine("\n  Force closing Autonauts...");
                    ForceCloseGame();
                    break;
                }
                else if (key == ConsoleKey.N)
                {
                    Console.WriteLine("N");
                    Console.WriteLine("\n  Waiting for you to close Autonauts...");
                    WaitForGameToClose();
                    break;
                }
            }
            
            // Also check if game was closed while waiting for input
            var currentProcesses = Process.GetProcessesByName("Autonauts");
            bool stillRunning = currentProcesses.Length > 0;
            foreach (var p in currentProcesses) p.Dispose();
            
            if (!stillRunning)
            {
                Console.WriteLine("(closed)");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  Autonauts was closed!");
                Console.ResetColor();
                break;
            }
            
            Thread.Sleep(100);
        }
        
        // Give the system a moment to release file handles
        Thread.Sleep(1000);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Autonauts is not running");
        Console.ResetColor();
    }
}

void ForceCloseGame()
{
    var processes = Process.GetProcessesByName("Autonauts");
    foreach (var process in processes)
    {
        try
        {
            process.CloseMainWindow();
            if (!process.WaitForExit(3000))
            {
                Console.WriteLine("  Game didn't close gracefully, killing process...");
                process.Kill();
                process.WaitForExit(2000);
            }
            process.Dispose();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed to close process: {ex.Message}");
            Console.ResetColor();
        }
    }
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  Game closed!");
    Console.ResetColor();
}

void WaitForGameToClose()
{
    int dots = 0;
    while (true)
    {
        var processes = Process.GetProcessesByName("Autonauts");
        bool stillRunning = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        
        if (!stillRunning)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Autonauts closed!");
            Console.ResetColor();
            break;
        }
        
        // Show waiting animation
        Console.Write($"\r  Waiting{new string('.', (dots % 3) + 1)}   ");
        dots++;
        
        Thread.Sleep(500);
    }
}
