namespace FrxConv.Common
{
    public class Configuration
    {
        public class User
        {
            public string? Username { get; set; }
            public string? Sid { get; set; }
            public string? ProfilePath { get; set; }
        }

        public const bool HideBuiltInAccounts = true;
        public const bool HideUnknownSiDs = true;
        public static bool CreateDynamicDisk = false;
        public static string? DeploymentDir;
        public static string? FullUserName;
        public static string? TargetDir;
        public static string? DiskSize;
        public static User? TargetUser;
    }
}
