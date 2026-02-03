using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AutonautsMP.Core
{
    /// <summary>
    /// Manages a separate console window for host debug output.
    /// Uses Windows P/Invoke to allocate and control a native console.
    /// </summary>
    public static class DebugConsole
    {
        // P/Invoke declarations for Windows console API
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleTitle(string lpConsoleTitle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, ushort wAttributes);

        private const int STD_OUTPUT_HANDLE = -11;

        // Console text colors
        private const ushort FOREGROUND_BLUE = 0x0001;
        private const ushort FOREGROUND_GREEN = 0x0002;
        private const ushort FOREGROUND_RED = 0x0004;
        private const ushort FOREGROUND_INTENSITY = 0x0008;
        private const ushort FOREGROUND_WHITE = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
        private const ushort FOREGROUND_CYAN = FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY;
        private const ushort FOREGROUND_YELLOW = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY;
        private const ushort FOREGROUND_MAGENTA = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_INTENSITY;

        private static bool _isOpen = false;
        private static IntPtr _consoleHandle = IntPtr.Zero;
        private static StreamWriter _consoleWriter;

        /// <summary>
        /// Whether the debug console is currently open.
        /// </summary>
        public static bool IsOpen => _isOpen;

        /// <summary>
        /// Opens the debug console window. Only works on Windows.
        /// </summary>
        public static void Show()
        {
            if (_isOpen) return;

            try
            {
                // Allocate a new console window
                if (AllocConsole())
                {
                    _consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                    SetConsoleTitle("AutonautsMP - Host Debug Console");

                    // Redirect Console.Out to the new console
                    var standardOutput = new StreamWriter(Console.OpenStandardOutput());
                    standardOutput.AutoFlush = true;
                    Console.SetOut(standardOutput);
                    _consoleWriter = standardOutput;

                    _isOpen = true;

                    // Print welcome message
                    WriteHeader();
                    Log("Debug console initialized");
                }
                else
                {
                    DebugLogger.Warning("Failed to allocate console window (not on Windows?)");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to open debug console: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the debug console window.
        /// </summary>
        public static void Hide()
        {
            if (!_isOpen) return;

            try
            {
                FreeConsole();
                _isOpen = false;
                _consoleHandle = IntPtr.Zero;
                _consoleWriter = null;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to close debug console: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes a header banner to the console.
        /// </summary>
        private static void WriteHeader()
        {
            SetColor(FOREGROUND_CYAN);
            Console.WriteLine("+================================================================+");
            Console.WriteLine("|              AUTONAUTSMP - HOST DEBUG CONSOLE                 |");
            Console.WriteLine("+================================================================+");
            Console.WriteLine("|  This window shows network events and debug information.      |");
            Console.WriteLine("|  Keep this window open while hosting.                         |");
            Console.WriteLine("+================================================================+");
            SetColor(FOREGROUND_WHITE);
            Console.WriteLine();
        }

        /// <summary>
        /// Log a message to the debug console.
        /// </summary>
        public static void Log(string message)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_WHITE);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
            catch { }
        }

        /// <summary>
        /// Log an info message (cyan).
        /// </summary>
        public static void LogInfo(string message)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_CYAN);
                Console.Write($"[{DateTime.Now:HH:mm:ss}] [INFO] ");
                SetColor(FOREGROUND_WHITE);
                Console.WriteLine(message);
            }
            catch { }
        }

        /// <summary>
        /// Log a warning message (yellow).
        /// </summary>
        public static void LogWarning(string message)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_YELLOW);
                Console.Write($"[{DateTime.Now:HH:mm:ss}] [WARN] ");
                SetColor(FOREGROUND_WHITE);
                Console.WriteLine(message);
            }
            catch { }
        }

        /// <summary>
        /// Log an error message (red).
        /// </summary>
        public static void LogError(string message)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_RED | FOREGROUND_INTENSITY);
                Console.Write($"[{DateTime.Now:HH:mm:ss}] [ERROR] ");
                SetColor(FOREGROUND_WHITE);
                Console.WriteLine(message);
            }
            catch { }
        }

        /// <summary>
        /// Log a network event (magenta).
        /// </summary>
        public static void LogNetwork(string message)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_MAGENTA);
                Console.Write($"[{DateTime.Now:HH:mm:ss}] [NET] ");
                SetColor(FOREGROUND_WHITE);
                Console.WriteLine(message);
            }
            catch { }
        }

        /// <summary>
        /// Log a packet event (green).
        /// </summary>
        public static void LogPacket(string direction, string packetType, string details = null)
        {
            if (!_isOpen) return;

            try
            {
                SetColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
                Console.Write($"[{DateTime.Now:HH:mm:ss}] [{direction}] ");
                SetColor(FOREGROUND_YELLOW);
                Console.Write(packetType);
                if (!string.IsNullOrEmpty(details))
                {
                    SetColor(FOREGROUND_WHITE);
                    Console.Write($" - {details}");
                }
                Console.WriteLine();
            }
            catch { }
        }

        /// <summary>
        /// Sets the console text color.
        /// </summary>
        private static void SetColor(ushort color)
        {
            if (_consoleHandle != IntPtr.Zero)
            {
                SetConsoleTextAttribute(_consoleHandle, color);
            }
        }
    }
}
