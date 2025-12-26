using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using static FrxConv.Common.Configuration;

namespace frxconv.Common
{
    public class Helper
    {
        public static List<User> GetUsersFromHost()
        {
            var users = new List<User>();
            var queryResult = RunPowerShellCommand(@"
                $ProgressPreference = 'SilentlyContinue'
                $host.UI.RawUI.BufferSize = new-object System.Management.Automation.Host.Size(1024,50)
                Get-WmiObject Win32_UserProfile | Select-Object SID, LocalPath"
            );

            if (!string.IsNullOrWhiteSpace(queryResult))
            {
                var lines = queryResult.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                bool headerSkipped = false;
                foreach (var line in lines)
                {
                    if (!headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }

                    var match = Regex.Match(line, @"^(S-1-[^ ]+)\s+([^\r\n]+)");
                    if (!match.Success) continue;

                    var sid = match.Groups[1].Value.TrimEnd();
                    var localPath = match.Groups[2].Value.TrimEnd();
                    var user = GetUserFromHost(sid, localPath);
                    if (user != null)
                    {
                        users.Add(user);
                    }
                }
            }

            return users;
        }

        private static User? GetUserFromHost(string sid, string localPath)
        {
            var username = GetUserByIdentity(sid);

            if (HideBuiltInAccounts && Regex.IsMatch(sid, @"^S-1-5-[0-9]+$"))
            {
                return null;
            }
            if (HideUnknownSiDs && sid == username)
            {
                return null;
            }

            return new User
            {
                Sid = sid,
                ProfilePath = localPath,
                Username = username
            };
        }

        private static string? GetUserByIdentity(string sid)
        {
            var output = RunPowerShellCommand($@"
                $ProgressPreference = 'SilentlyContinue'
                $host.UI.RawUI.BufferSize = new-object System.Management.Automation.Host.Size(1024,50)
                $ntAccount = (New-Object System.Security.Principal.SecurityIdentifier(""{sid}"")).Translate([System.Security.Principal.NTAccount])
                $ntAccount.Value | Out-String -Width 4096
            ").Trim();

            return string.IsNullOrWhiteSpace(output) ? sid : output;
        }

        private static string RunPowerShellCommand(string command)
        {
            var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -NoLogo -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process();
            process.StartInfo = psi;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("PowerShell error: " + error);

            }
            process.WaitForExit();

            return output;
        }

        public static bool IsUncPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                return uri.IsUnc;
            }

            // Additional manual check for UNC paths that might not be caught by Uri.TryCreate
            return path.StartsWith(@"\\") && path.Trim('\\').Contains(@"\");
        }

        public static byte[] SidToBinary(string sidString)
        {
            SecurityIdentifier sid = new SecurityIdentifier(sidString);
            byte[] binaryForm = new byte[sid.BinaryLength];
            sid.GetBinaryForm(binaryForm, 0);
            return binaryForm;
        }

        public static string ConvertToRegHex(object input, bool isUnicodeString)
        {
            byte[] bytes;

            if (isUnicodeString && input is string str)
            {
                bytes = Encoding.Unicode.GetBytes(str + "\0");
            }
            else if (input is byte[] byteArray)
            {
                bytes = byteArray;
            }
            else
            {
                throw new ArgumentException("Invalid input type. Expected string for Unicode or byte array.");
            }

            var sb = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                sb.AppendFormat("{0:X2}", bytes[i]);
                if (i >= bytes.Length - 1) continue;

                sb.Append(",");
                if ((i + 1) % 16 == 0)
                    sb.Append("\\\n  ");
            }

            return sb.ToString();
        }

        public static string? GetFreeLetter()
        {
            var availableDriveLetters = new List<char>() { 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            var drives = DriveInfo.GetDrives();
            foreach (var t in drives)
            {
                availableDriveLetters.Remove(t.Name.ToLower()[0]);
            }
            var freeDisks = availableDriveLetters.ToArray();
            return $"{freeDisks[0]}";
        }

        public static int StartProcess(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process!.WaitForExit();
            return process.ExitCode;
        }
    }
}
