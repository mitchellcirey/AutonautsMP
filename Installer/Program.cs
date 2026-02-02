using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

class Program
{
    const string BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x64_5.4.21.0.zip";

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("     AutonautsMP Installer");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Find game path
        string? gamePath = FindAutonautsPath();
        if (string.IsNullOrEmpty(gamePath))
        {
            Console.Write("Enter Autonauts folder path: ");
            gamePath = Console.ReadLine()?.Trim().Trim('"');
        }

        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            Console.WriteLine("ERROR: Invalid path.");
            Pause();
            return 1;
        }

        Console.WriteLine($"Game folder: {gamePath}");
        Console.WriteLine();

        // Find mod files (same folder as installer, or mod/ subfolder)
        string? modDir = FindModFiles();
        if (modDir == null)
        {
            Console.WriteLine("ERROR: Mod files not found!");
            Console.WriteLine("Place AutonautsMP.dll, AutonautsMP.Network.dll, and LiteNetLib.dll");
            Console.WriteLine("in the same folder as this installer.");
            Pause();
            return 1;
        }

        // Check if BepInEx is installed
        string bepinexCore = Path.Combine(gamePath, "BepInEx", "core", "BepInEx.dll");
        if (!File.Exists(bepinexCore))
        {
            Console.WriteLine("BepInEx not found. Downloading...");
            if (!await DownloadAndInstallBepInEx(gamePath))
            {
                Console.WriteLine("ERROR: Failed to install BepInEx.");
                Pause();
                return 1;
            }
            Console.WriteLine("BepInEx installed!");
        }
        else
        {
            Console.WriteLine("BepInEx already installed.");
        }

        Console.WriteLine();
        Console.WriteLine("Installing AutonautsMP...");

        // Install main plugin to BepInEx/plugins
        string pluginDir = Path.Combine(gamePath, "BepInEx", "plugins", "AutonautsMP");
        Directory.CreateDirectory(pluginDir);
        File.Copy(Path.Combine(modDir, "AutonautsMP.dll"), Path.Combine(pluginDir, "AutonautsMP.dll"), true);
        Console.WriteLine("  - Installed AutonautsMP.dll");

        // Install network DLLs to AppData
        string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutonautsMP");
        Directory.CreateDirectory(appDataDir);
        File.Copy(Path.Combine(modDir, "AutonautsMP.Network.dll"), Path.Combine(appDataDir, "AutonautsMP.Network.dll"), true);
        File.Copy(Path.Combine(modDir, "LiteNetLib.dll"), Path.Combine(appDataDir, "LiteNetLib.dll"), true);
        Console.WriteLine("  - Installed network DLLs to AppData");

        Console.WriteLine();
        Console.WriteLine("===========================================");
        Console.WriteLine("  Installation complete!");
        Console.WriteLine("===========================================");
        Console.WriteLine();
        Console.WriteLine("Launch Autonauts and click the MP button in the top-right corner.");
        Console.WriteLine();

        Pause();
        return 0;
    }

    static string? FindAutonautsPath()
    {
        // Try common Steam paths
        string[] tryPaths = {
            @"C:\Program Files (x86)\Steam\steamapps\common\Autonauts",
            @"C:\Program Files\Steam\steamapps\common\Autonauts",
            @"D:\Steam\steamapps\common\Autonauts",
            @"D:\SteamLibrary\steamapps\common\Autonauts",
            @"E:\SteamLibrary\steamapps\common\Autonauts",
        };

        foreach (var path in tryPaths)
        {
            if (File.Exists(Path.Combine(path, "Autonauts.exe")))
                return path;
        }

        // Try Steam registry
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            string? steamPath = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(steamPath))
            {
                string common = Path.Combine(steamPath, "steamapps", "common", "Autonauts");
                if (File.Exists(Path.Combine(common, "Autonauts.exe")))
                    return common;
            }
        }
        catch { }

        return null;
    }

    static string? FindModFiles()
    {
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        // Check same folder as installer
        if (File.Exists(Path.Combine(exeDir, "AutonautsMP.dll")) &&
            File.Exists(Path.Combine(exeDir, "AutonautsMP.Network.dll")) &&
            File.Exists(Path.Combine(exeDir, "LiteNetLib.dll")))
            return exeDir;

        // Check mod/ subfolder
        string modSub = Path.Combine(exeDir, "mod");
        if (File.Exists(Path.Combine(modSub, "AutonautsMP.dll")) &&
            File.Exists(Path.Combine(modSub, "AutonautsMP.Network.dll")) &&
            File.Exists(Path.Combine(modSub, "LiteNetLib.dll")))
            return modSub;

        return null;
    }

    static async Task<bool> DownloadAndInstallBepInEx(string gamePath)
    {
        string zipPath = Path.Combine(Path.GetTempPath(), "BepInEx_temp.zip");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "AutonautsMP-Installer");

            Console.WriteLine("  Downloading BepInEx 5.4.21...");
            var response = await http.GetAsync(BepInExUrl);
            response.EnsureSuccessStatusCode();

            await using var fs = File.Create(zipPath);
            await response.Content.CopyToAsync(fs);
            fs.Close();

            Console.WriteLine("  Extracting...");
            ZipFile.ExtractToDirectory(zipPath, gamePath, true);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
            return false;
        }
        finally
        {
            try { File.Delete(zipPath); } catch { }
        }
    }

    static void Pause()
    {
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey(true);
    }
}
