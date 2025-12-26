using frxconv.Common;
using Spectre.Console;
using System.Diagnostics;
using static FrxConv.Common.Configuration;

namespace FrxConv
{
    internal class Program
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
                    AnsiConsole.Markup("[red bold]FrxConv[/] {0}\nCopyright (c) 2018 - 2026 [link=https://valnoxy.dev]valnoxy[/]. All rights reserved.\n\n", Markup.Escape("[Version 2.0]"));
                    RunMigration();
                    break;
            }

        }
        private static void ShowHelp()
        {
            AnsiConsole.Markup("[red bold]FrxConv[/] {0}\nCopyright (c) 2018 - 2026 [link=https://valnoxy.dev]valnoxy[/]. All rights reserved.\n\n", Markup.Escape("[Version 2.0]"));
            AnsiConsole.MarkupLine("[bold gray]Syntax[/]:");
            AnsiConsole.MarkupLine("  frxconv.exe {0} {1} {2} (-dynamic)",
                Markup.Escape("[Domain\\Username]"),
                Markup.Escape("[Path\\To\\Store]"),
                Markup.Escape("[Disk Size in MB]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold gray]Options[/]:");
            AnsiConsole.MarkupLine("  {0}       Define the user you want to migrate.", Markup.Escape("[Domain\\Username]"));
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("  {0}         File path to the destination of the virtual disk.", Markup.Escape("[Path\\To\\Store]"));
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
            var profileData = "";
            var targetUserPath = "";
            AnsiConsole.Status()
                .Start("Preparing migration ...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));

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
                        return;
                    }
                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Found target user on system. Proceed with SID [bold blue]{TargetUser.Sid}[/] ...");

                    // Check target path
                    if (Helper.IsUncPath(TargetDir!))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: UNC paths are currently not supported. Please mount your network share before migrating this user.");
                        return;
                    }

                    if (!Directory.Exists(TargetDir))
                    {
                        AnsiConsole.MarkupLine($"[bold red]ERROR[/]: Directory '{TargetDir}' not found.");
                        return;
                    }

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
                        return;
                    }

                    if (File.Exists(targetUserPath))
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: There is already a [blue]profile disk[/] for this user. Please remove it first to continue.");
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
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: [blue]Profile Disk[/] successfully created.");

                    // Set permissions
                    Directory.CreateDirectory($"{deploymentLetter}:\\Profile");
                    var status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /inheritance:r");
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Disabled [blue]inheritance[/] for Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant SYSTEM:(OI)(CI)F"); // System
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Granted [blue]SYSTEM[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant *S-1-5-32-544:(OI)(CI)F"); // Administrators
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Granted [blue]Administrators[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /grant {FullUserName}:(OI)(CI)F"); // User itself
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        return;
                    }
                    AnsiConsole.MarkupLine($"[grey bold]INFO[/]: Granted [blue]User {FullUserName}[/] access to Profile directory.");

                    status = Helper.StartProcess("icacls", $@"{deploymentLetter}:\Profile /setowner SYSTEM");
                    if (status != 0)
                    {
                        AnsiConsole.MarkupLine("[bold red]ERROR[/]: Failed to run icacls: Exited with code " + status);
                        return;
                    }
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: Changed Ownership from Profile directory to [blue]SYSTEM[/].");

                    Directory.CreateDirectory($"{deploymentLetter}:\\Profile\\AppData\\Local\\FSLogix");
                    File.WriteAllText($"{deploymentLetter}:\\Profile\\AppData\\Local\\FSLogix\\ProfileData.reg", profileData);
                    AnsiConsole.MarkupLine("[grey bold]INFO[/]: [blue]ProfileData[/] has been written to [blue]Profile Disk[/].");
                    success = true;
                });
            if (success == false)
            {
                AnsiConsole.MarkupLine("[bold red]ERROR[/]: Profile Migration failed!");
                Environment.Exit(1);
            }

            // Data Migration
            AnsiConsole.Progress()
                .Start(ctx =>
                {
                    var migTask = ctx.AddTask("[green]Migrating user files[/]");
                    migTask.StartTask();
                    success = UserProfileMigration.MigrateUserProfile(TargetUser!.ProfilePath!, DeploymentDir!, migTask, ctx);
                    migTask.StopTask();
                    var diffTask = migTask.StopTime - migTask.StartTime;
                    AnsiConsole.MarkupLine($@"[grey bold]INFO[/]: [blue]Data Migration[/] completed in [blue]{diffTask:hh\:mm\:ss}[/].");

                    // Write profile data
                    var profileDataTask = ctx.AddTask("[green]Writing ProfileData registry to Profile Disk[/]");
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

                    // Detach vdisk
                    var detachVdisk = ctx.AddTask("[green]Detach Profile Disk[/]");
                    detachVdisk.IsIndeterminate = true;
                    detachVdisk.StartTask();
                    var partDest = new Process();
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
                    detachVdisk.Value = 100;
                    detachVdisk.StopTask();
                });

            // Completed
            AnsiConsole.MarkupLine(success
                ? "[bold green]DONE[/]: Profile Migration completed!"
                : "[bold red]ERROR[/]: Profile Migration failed!");

            Environment.Exit(success ? 0 : 1);
        }
    }
}
