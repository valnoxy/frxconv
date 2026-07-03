using frxconv.Common;
using Spectre.Console;
using System.Diagnostics;
using static FrxConv.Common.Configuration;

namespace FrxConv
{
    public class Program
    {
        static void Main(string[] args)
        {
            switch (args.Length)
            {
                default:
                    ShowHelp();
                    break;
                case 3:
                case 4:
                    FullUserName = args[0];
                    TargetDir = args[1];
                    DiskSize = args[2];
                    foreach (var arg in args)
                    {
                        if (arg.Equals("-dynamic", StringComparison.CurrentCultureIgnoreCase))
                            CreateDynamicDisk = true;
                    }
                    AnsiConsole.Markup("[red bold]FrxConv[/] {0}\nCopyright (c) 2018 - 2026 [link=https://valnoxy.dev]valnoxy[/]. All rights reserved.\n\n", Markup.Escape("[Version 2.1]"));
                    RunMigration();
                    break;
            }
        }

        private static void ShowHelp()
        {
            AnsiConsole.Markup("[red bold]FrxConv[/] {0}\nCopyright (c) 2018 - 2026 [link=https://valnoxy.dev]valnoxy[/]. All rights reserved.\n\n", Markup.Escape("[Version 2.1]"));
            AnsiConsole.MarkupLine("[bold gray]Syntax[/]:");
            AnsiConsole.MarkupLine("  frxconv.exe {0} {1} {2} (-dynamic)",
                Markup.Escape("[Domain\\Username]"),
                Markup.Escape("[Path\\To\\Store]"),
                Markup.Escape("[Disk Size in MB]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold gray]Options[/]:");
            AnsiConsole.MarkupLine("  {0}       Define the user you want to migrate.", Markup.Escape("[Domain\\Username]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("  {0}         File path to the destination of the virtual disk (UNC Path not supported).", Markup.Escape("[Path\\To\\Store]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("  {0}       Size of the virtual disk in MB.", Markup.Escape("[Disk Size in MB]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("  -dynamic                Create a dynamic virtual disk.");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold gray]Example[/]:");
            AnsiConsole.MarkupLine("  frxconv.exe Contoso\\John.Doe D:\\FSLogixStore 30720 -dynamic");
            Environment.Exit(1);
        }

        private static void RunMigration()
        {
            var success = false;
            var aborted = false;
            var profileData = "";
            var targetUserPath = "";
            var snapshotProfilePath = "";
            var snapshotIdBuffer = new char[64];
            var devicePathBuffer = new char[512];

            try
            {
                AnsiConsole.Status()
                .Start("Preparing migration ...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    // Disable DiskMgrLib logging
                    DiskMgr.DiskMgr_SetSilentMode(true);

                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Fetching user list ...");
                    var data = Helper.GetUsersFromHost();
                    foreach (var user in data!)
                    {
                        AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Got [blue]{user.Username}[/] ([gray]{user.Sid}[/] -> [gray]{user.ProfilePath}[/])");
                        if (string.Equals(user.Username!, FullUserName, StringComparison.CurrentCultureIgnoreCase))
                            TargetUser = user;
                    }
                    if (TargetUser == null)
                    {
                        AnsiConsole.MarkupLine($"[bold red]ERROR[/]: User {FullUserName} not found!");
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Found target user [bold blue]\"{TargetUser.Username}\"[/]. Proceed with SID [bold blue]{TargetUser.Sid}[/] ...");

                    // Check target path
                    if (Helper.IsUncPath(TargetDir!))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: UNC paths are currently not supported. Please mount your network share before migrating this user.");
                        aborted = true;
                        return;
                    }

                    if (!Directory.Exists(TargetDir))
                    {
                        AnsiConsole.MarkupLine($"[bold red]ERROR[/]: Directory '{TargetDir}' not found.");
                        aborted = true;
                        return;
                    }

                    // Create VSS snapshot
                    ctx.Status("Creating Volume Shadow snapshot ...");
                    var hrVssSnapshot = DiskMgr.DiskMgr_CreateVssSnapshot(@"C:\", snapshotIdBuffer, snapshotIdBuffer.Length, devicePathBuffer, devicePathBuffer.Length);
                    if (hrVssSnapshot != 0) // S_OK
                    {
                        AnsiConsole.MarkupLine($"[bold red]ERROR[/]: Failed to create Volume Shadow snapshot, HRESULT: 0x{hrVssSnapshot:X8}");
                        aborted = true;
                        return;
                    }
                    var snapshotId = new string(snapshotIdBuffer).TrimEnd('\0');
                    var devicePath = new string(devicePathBuffer).TrimEnd('\0');
                    var userPathWithoutDrive = TargetUser!.ProfilePath!.Substring(2);
                    snapshotProfilePath = Path.Join(devicePath, userPathWithoutDrive);

                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Created [blue]Volume Shadow snapshot ({snapshotId})[/] -> [gray]{snapshotProfilePath}[/]");

                    // Build profile data
                    var profileImagePath = Helper.ConvertToRegHex(TargetUser.ProfilePath!, true);
                    var sidBytes = Helper.SidToBinary(TargetUser.Sid!);
                    var sidHex = BitConverter.ToString(sidBytes).Replace("-", ",");
                    profileData = string.Join(Environment.NewLine,
                        "Windows Registry Editor Version 5.00",
                        $"[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\{TargetUser.Sid}]",
                        $"\"ProfileImagePath\"=hex(2):{profileImagePath}",
                        $"\"FSL_OriginalProfileImagePath\"=\"{TargetUser.ProfilePath}\"",
                        "\"Flags\"=dword:00000000",
                        "\"State\"=dword:00000000",
                        $"\"Sid\"=hex:{sidHex}",
                        "\"ProfileLoadTimeLow\"=dword:00000000",
                        "\"ProfileLoadTimeHigh\"=dword:00000000",
                        "\"RefCount\"=dword:00000000",
                        "\"RunLogonScriptSync\"=dword:00000000"
                    );
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Stored new profile data to memory.");

                    // User Disk
                    ctx.Status("Building user disk ...");
                    var userSplit = FullUserName!.Split("\\");
                    var userName = userSplit[1];
                    targetUserPath = Path.Combine(TargetDir!, $"{TargetUser.Sid}_{userName}", $"Profile_{userName}.vhdx");
                    var deploymentLetter = Helper.GetFreeLetter();
                    if (Directory.Exists($"{deploymentLetter}:\\"))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: No free drive letter available on this system.");
                        aborted = true;
                        return;
                    }

                    if (File.Exists(targetUserPath))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: There is already a [blue]profile disk[/] for this user. Please remove it first to continue.");
                        aborted = true;
                        return;
                    }

                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Creating [blue]Profile Disk[/] -> [gray]{targetUserPath}[/]");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetUserPath)!);
                    DeploymentDir = $"{deploymentLetter}:\\Profile";
                    var partDest = new Process();
                    partDest.StartInfo.FileName = "diskpart.exe";
                    partDest.StartInfo.UseShellExecute = false;
                    partDest.StartInfo.CreateNoWindow = true;
                    partDest.StartInfo.RedirectStandardInput = true;
                    partDest.StartInfo.RedirectStandardOutput = true;
                    partDest.Start();

                    partDest.StandardInput.WriteLine(CreateDynamicDisk
                        ? $"create vdisk file=\"{targetUserPath}\" maximum={DiskSize} type=expandable"
                        : $"create vdisk file=\"{targetUserPath}\" maximum={DiskSize} type=fixed");
                    partDest.StandardInput.WriteLine($"select vdisk file=\"{targetUserPath}\"");
                    partDest.StandardInput.WriteLine("attach vdisk");
                    partDest.StandardInput.WriteLine("create partition primary");
                    partDest.StandardInput.WriteLine("convert gpt");
                    partDest.StandardInput.WriteLine($"format fs=ntfs quick label=\"Profile-{userName}\"");
                    partDest.StandardInput.WriteLine($"assign letter={deploymentLetter}");
                    partDest.StandardInput.WriteLine("exit");
                    partDest.WaitForExit();
                    if (!Directory.Exists($"{deploymentLetter}:\\"))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to create [blue]Profile Disk[/].");
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: [blue]Profile Disk[/] successfully created.");

                    // Set permissions
                    Directory.CreateDirectory($"{deploymentLetter}:\\Profile");
                    var status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /inheritance:r");
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Disabled [blue]inheritance[/] for Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant SYSTEM:(OI)(CI)F"); // System
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Granted [blue]SYSTEM[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant *S-1-5-32-544:(OI)(CI)F"); // Administrators
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Granted [blue]Administrators[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant {FullUserName}:(OI)(CI)F"); // User itself
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Granted [blue]User {FullUserName}[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /setowner SYSTEM");
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        aborted = true;
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Changed Ownership from Profile directory to [blue]SYSTEM[/].");

                    //Directory.CreateDirectory($"{deploymentLetter}:\\Profile\\AppData\\Local\\FSLogix");
                    //File.WriteAllText($"{deploymentLetter}:\\Profile\\AppData\\Local\\FSLogix\\ProfileData.reg", profileData);
                    //AnsiConsole.MarkupLine("[grey bold]INFO[/]: [blue]ProfileData[/] has been written to [blue]Profile Disk[/].");

                    if (aborted) return;
                    success = true;
                });

                if (aborted || !success)
                {
                    AnsiConsole.MarkupLine("[bold red]ERROR[/]: Profile Migration failed!");
                    return;
                }

                // Data Migration
                AnsiConsole.Progress()
                    .Start(progressContext =>
                    {
                        var migTask = progressContext.AddTask("[green]Migrating user files[/]");
                        migTask.StartTask();

                        success = UserProfileMigration.MigrateUserProfile(snapshotProfilePath, DeploymentDir!, migTask, progressContext);

                        migTask.StopTask();
                        var diffTask = migTask.StopTime - migTask.StartTime;
                        AnsiConsole.MarkupLine($@"[grey bold]INFO[/]: [blue]Data Migration[/] completed in [blue]{diffTask:hh\:mm\:ss}[/].");

                        var profileDataTask = progressContext.AddTask("[green]Writing ProfileData registry to Profile Disk[/]");
                        profileDataTask.IsIndeterminate = true;
                        profileDataTask.StartTask();
                        Directory.CreateDirectory($"{DeploymentDir}\\AppData\\Local\\FSLogix");
                        if (!File.Exists($"{DeploymentDir}\\AppData\\Local\\FSLogix\\ProfileData.reg"))
                        {
                            File.WriteAllText($"{DeploymentDir}\\AppData\\Local\\FSLogix\\ProfileData.reg", profileData);
                            AnsiConsole.MarkupLine("[grey bold]INFO[/]: [blue]ProfileData[/] has been written to [blue]Profile Disk[/].");
                        }
                        else AnsiConsole.MarkupLine("[yellow bold]WARN[/]: [blue]ProfileData[/] already exists on [blue]Profile Disk[/]. Skipping ...");
                        profileDataTask.Value = 100;
                        profileDataTask.StopTask();

                        var detachVdisk = progressContext.AddTask("[green]Detach Profile Disk[/]");
                        detachVdisk.IsIndeterminate = true;
                        detachVdisk.StartTask();
                        using (var partDest = new Process())
                        {
                            partDest.StartInfo.FileName = "diskpart.exe";
                            partDest.StartInfo.UseShellExecute = false;
                            partDest.StartInfo.CreateNoWindow = true;
                            partDest.StartInfo.RedirectStandardInput = true;
                            partDest.StartInfo.RedirectStandardOutput = true;
                            partDest.Start();
                            partDest.StandardInput.WriteLine($"select vdisk file=\"{targetUserPath}\"");
                            partDest.StandardInput.WriteLine("select partition 1");
                            partDest.StandardInput.WriteLine("remove all");
                            partDest.StandardInput.WriteLine("detach vdisk");
                            partDest.StandardInput.WriteLine("exit");
                            partDest.WaitForExit();
                        }
                        detachVdisk.Value = 100;
                        detachVdisk.StopTask();
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red bold]ERROR[/]: Something went wrong: {Markup.Escape(ex.ToString())}");
            }
            finally
            {
                var snapshotId = new string(snapshotIdBuffer).TrimEnd('\0');
                if (!string.IsNullOrEmpty(snapshotId))
                {
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Disposing [blue]Volume Shadow Copy[/] ...");
                    var hr = DiskMgr.DiskMgr_DeleteVssSnapshot(snapshotId);
                    if (hr != 0)
                    {
                        AnsiConsole.MarkupLine($"[bold red]ERROR[/]: Failed to dispose Volume Shadow snapshot, HRESULT: 0x{hr:X8}");
                    }
                }
            }
            // Completed
            AnsiConsole.MarkupLine(success
                ? "[bold green]DONE[/]: Profile Migration completed!"
                : "[bold red]ERROR[/]: Profile Migration failed!");
            Environment.Exit(success ? 0 : 1);
        }
    }
}
