using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;

namespace FirefoxShortcut
{
    internal class Program
    {

        /// <summary>
        /// Checks if the application is currently running under an Administrator account.
        /// </summary>
        private static bool IsRunAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Spawns a new instance of this exact executable requesting UAC elevation.
        /// </summary>
        /// <returns>True if the process started successfully; False if denied by the user.</returns>
        private static bool ElevateProcess() {
            string currentExe = Environment.ProcessPath;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true,
                Verb = "runas" // Triggers the Windows UAC Prompt
            };

            try
            {
                Process.Start(startInfo);
                return true; // Successfully launched elevated instance
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false; // User clicked "No" on the UAC dialog
            }
        }

        public static bool AskYesNo(string prompt, bool defaultNo = false)
        {
            Console.Write($"{prompt} {(defaultNo ? "(y/N)" : "(Y/n)")} ");

            string? input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(input))
                return !defaultNo;

            return input switch
            {
                "y" or "yes" or "yep" or "yeah" => true,
                "n" or "no" or "nah" or "nope" => false,
                _ => defaultNo
            };
        }

        public static void Convert(string path, string launcher)
        {
            Console.WriteLine("Converting launcher " + launcher + " into: " + path);

            string dest = Path.Combine(path, "launcher.exe");
            if (File.Exists(dest))
            {
                bool replace = AskYesNo("launcher.exe file already exists. Do you want to replace?\n(No will ask you for another directory.)");
                if (replace)
                {
                    File.Delete(dest);
                } else
                {
                    path = GetDirectory(true);
                    Console.WriteLine();
                    Convert(path, launcher);
                    return;
                }
            }

            while (true)
            {
                try { File.Move(launcher, dest); }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to move: " + e.ToString());
                    if (AskYesNo("Try again?")) continue;
                    else return;
                }

                break;
            }

            Console.WriteLine("Launcher moved to launcher.exe inside path.");

            while (true)
            {
                try {
                    IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(Path.Combine(Path.GetDirectoryName(launcher), "Firefox.lnk"));

                    shortcut.TargetPath = dest;
                    shortcut.WorkingDirectory = path;
                    shortcut.Description = "Launch Firefox";

                    shortcut.Save();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to create shortcut: " + e.ToString());
                    if (AskYesNo("Try again?")) continue;
                    else return;
                }

                break;
            }

            Console.WriteLine("Launcher shortcut created! You now should be able to open Firefox from the shortcut.");

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        public static void Convert() => Convert(GetDirectory(), GetLauncher());

        public static string GetDirectory(bool alreadyInvaild = false)
        {
            string dir = "";
            
            if (!alreadyInvaild) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox");
            if (alreadyInvaild || !Directory.Exists(dir))
            {
                Console.WriteLine("If you would like to move the launcher elsewhere, please type that path below.");
                Console.WriteLine("If not, press enter now (with nothing) to exit.");

                while (true)
                {
                    string? line = Console.ReadLine()?.Trim();

                    if (String.IsNullOrEmpty(line))
                    {
                        Console.WriteLine("Goodbye!");
                        Environment.Exit(1);
                    }

                    if (!Directory.Exists(line))
                    {
                        Console.WriteLine("That either isn't a directory or doesnt exist. Try again.");
                        Console.WriteLine("As always, you can enter nothing to exit.");
                        continue;
                    }

                    dir = line;
                    break;
                }
            }

            return dir;
        }

        public static string GetLauncher()
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Firefox.exe");

            if (!File.Exists(file))
            {
                Console.WriteLine("I could not find Firefox.exe at the desktop.");
                Console.WriteLine("If you would like to convert the launcher from elsewhere, please type that path below.");
                Console.WriteLine("If not, press enter now (with nothing) to exit.");

                while (true)
                {
                    string? line = Console.ReadLine()?.Trim();

                    if (String.IsNullOrEmpty(line))
                    {
                        Console.WriteLine("Goodbye!");
                        Environment.Exit(1);
                    }

                    if (!Directory.Exists(line))
                    {
                        Console.WriteLine("That either isn't a file or doesnt exist. Try again.");
                        Console.WriteLine("As always, you can enter nothing to exit.");
                        continue;
                    }

                    file = line;
                    break;
                }
            }

            return file;
        }

        static void Main(string[] args)
        {
            if (!IsRunAsAdmin()) {
                if (!ElevateProcess())
                {
                    Console.WriteLine("This application needs admin permissions to copy to the Firefox directory.");
                    Convert(GetDirectory(true), GetLauncher());
                } else
                {
                    return;
                }
            }

            Convert();
        }
    }
}
