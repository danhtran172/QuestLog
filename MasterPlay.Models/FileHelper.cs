using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MasterPlay.Helpers
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
                        // Open folder
                        Process.Start("explorer.exe", path);
                    }
                    else if (File.Exists(path))
                    {
                        // Open containing folder and select the file
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

    }
}