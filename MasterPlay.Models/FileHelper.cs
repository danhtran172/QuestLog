using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MasterPlay.Helpers
{
    public static class FileHelper
    {
        public static bool TryOpenLocations(out List<string> errors, params (string? Path, string Desc)[] locations)
        {
            errors = new List<string>();

            if (locations == null || locations.Length == 0)
            {
                return true;
            }

            foreach (var loc in locations)
            {
                string? path = loc.Path;
                string desc = loc.Desc;

                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add($"{desc}: Đường dẫn trống hoặc null.");
                    continue;
                }

                path = path.Trim();

                try
                {
                    if (Directory.Exists(path))
                    {
                        // Mở thư mục
                        Process.Start("explorer.exe", path);
                    }
                    else if (File.Exists(path))
                    {
                        // Mở file (mở thư mục chứa file)
                        string folder = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        {
                            Process.Start("explorer.exe", $"/select,\"{path}\"");
                        }
                        else
                        {
                            errors.Add($"{desc}: Thư mục chứa file không tồn tại ({path}).");
                        }
                    }
                    else
                    {
                        errors.Add($"{desc}: Đường dẫn không tồn tại ({path}).");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{desc}: Lỗi khi mở ({path}): {ex.Message}");
                }
            }

            return errors.Count == 0;
        }
    }
}