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
            "AppData\\Local\\Google\\Chrome\\User Data\\Default\\Cache",
            "AppData\\Local\\CrashDumps",
            "AppData\\Local\\Microsoft\\Windows\\WER",
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
            CopyDirectory(source, destination, ExcludedPaths, task, ctx);
            return true;
        }

        private static void CopyDirectory(string sourceDir, string destDir, string[]? excludePaths, ProgressTask task, ProgressContext ctx)
        {
            if (!Directory.Exists(sourceDir)) return;

            Directory.CreateDirectory(destDir);

            var files = Directory.GetFiles(sourceDir);
            task.MaxValue = files.Length + Directory.GetDirectories(sourceDir).Length;
            foreach (var file in files)
            {
                try
                {
                    var destFile = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    task.Increment(1);
                }
                catch (UnauthorizedAccessException)
                {
                    AnsiConsole.MarkupLine($"[red bold]ERROR[/]: Cannot access file [blue]{file}[/]!");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red bold]ERROR[/]: Failed to copy file [blue]{file}[/]!");
                }
            }

            var directories = Directory.GetDirectories(sourceDir);
            foreach (var dir in directories)
            {
                var relativePath = dir[(sourceDir.Length + 1)..];
                if (excludePaths != null && Array.Exists(excludePaths, p => relativePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    continue;
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
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)), excludePaths, task, ctx);
                task.Increment(1);
            }
        }
    }
}
