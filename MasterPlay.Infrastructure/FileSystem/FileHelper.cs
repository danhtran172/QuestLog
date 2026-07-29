using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MasterPlay.Infrastructure.FileSystem
{
    public static class FileHelper
    {
        public static bool TryOpenLocations(out List<string> errors, params (string? Path, string Description)[] locations)
        {
            errors = new List<string>();

            if (locations == null || locations.Length == 0)
            {
                return true;
            }

            foreach (var loc in locations)
            {
                string? path = loc.Path;
                string description = loc.Description;

                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add($"{description}: Path is null or empty");
                    continue;
                }

                path = path.Trim();

                try
                {
                    if (Directory.Exists(path))
                    {

                        Process.Start("explorer.exe", path);
                    }
                    else if (File.Exists(path))
                    {

                        string? folder = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        {
                            Process.Start("explorer.exe", $"/select,\"{path}\"");
                        }
                        else
                        {
                            errors.Add($"{description}: Containing folder does not exist ({path}).");
                        }
                    }
                    else
                    {
                        errors.Add($"{description}: Path does not exist ({path}).");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{description}: Error opening path ({path}): {ex.Message}");
                }
            }

            return errors.Count == 0;
        }
        public static bool FileCheck(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = path.Trim();

            try
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void RunProcess(string path)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("[ERROR] Launch failed: Path is null or empty.");
                    return;
                }

                if (!File.Exists(path))
                {
                    Console.WriteLine($"[ERROR] Launch failed: File does not exist at '{path}'.");
                    return;
                }


                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = path,

                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Console.WriteLine($"[INFO] Initializing process: {Path.GetFileName(path)}");


                using (Process? exeProcess = Process.Start(startInfo))
                {
                    if (exeProcess != null)
                    {
                        Console.WriteLine($"[SUCCESS] Process started successfully (PID: {exeProcess.Id}).");
                    }
                    else
                    {
                        Console.WriteLine("[WARNING] Process started but handle is null.");
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {

                Console.WriteLine($"[ERROR] System/Permission error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
            }
        }

    }
}