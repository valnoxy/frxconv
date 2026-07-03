using Spectre.Console;

namespace frxconv.Common
{
    class UserProfileMigration
    {
        static readonly string[]? ExcludedPaths =
        [
            "AppData\\Local\\Temp",
            "AppData\\Local\\Microsoft\\Windows\\Explorer",
            "AppData\\Local\\Microsoft\\Windows\\INetCache",
            "AppData\\Local\\Microsoft\\Windows\\WER",
            "AppData\\Local\\Microsoft\\WindowsApps",
            "AppData\\Local\\Google\\Chrome\\User Data\\Default\\Cache",
            "AppData\\Local\\CrashDumps",
            "AppData\\Local\\NVIDIA\\DXCache",
            "AppData\\Roaming\\Microsoft\\Windows\\Recent",
            ".vscode\\extensions",
            ".android\\avd",
            ".gradle\\caches",
            ".nuget\\packages"
        ];

        public static bool MigrateUserProfile(string source, string destination, ProgressTask task, ProgressContext ctx)
        {
            if (!Directory.Exists(source))
            {
                AnsiConsole.MarkupLine("[red bold]ERROR[/]: Source directory does not exist!");
                return false;
            }

            task.IsIndeterminate = false;
            task.MaxValue = 100;
            task.Value = 0;
            
            CopyDirectory(source, source, destination, ExcludedPaths, task, ctx);

            task.Value = 100;
            return true;
        }

        private static void CopyDirectory(string rootSourceDir, string currentSourceDir, string destDir, string[]? excludePaths, ProgressTask task, ProgressContext ctx)
        {
            if (!Directory.Exists(currentSourceDir)) return;

            Directory.CreateDirectory(destDir);

            // Find files in the current directory and copy them to the destination
            string[] files;
            try
            {
                files = Directory.GetFiles(currentSourceDir);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow bold]WARN[/]: Cannot read files from [blue]{currentSourceDir}[/]: {ex.Message}");
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    var destFile = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);

                    if (task.Value < 99) task.Increment(0.05);
                }
                catch (UnauthorizedAccessException)
                {
                    AnsiConsole.MarkupLine($"[red bold]ERROR[/]: Cannot access file [blue]{file}[/]!");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red bold]ERROR[/]: Failed to copy file [blue]{file}[/]!");
                    AnsiConsole.MarkupLine($"[red bold]ERROR[/]: {ex.Message}");
                }
            }

            // Get directories and recursively copy them
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(currentSourceDir);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(rootSourceDir, dir);
                if (excludePaths != null && Array.Exists(excludePaths, p => relativePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                try
                {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red bold]WARN[/]: Failed to check object [blue]{dir}[/]: {ex.Message}");
                    continue;
                }

                CopyDirectory(rootSourceDir, dir, Path.Combine(destDir, Path.GetFileName(dir)), excludePaths, task, ctx);
            }
        }
    }
}