using System.Runtime.InteropServices;

namespace frxconv.Common
{
    public partial class DiskMgr
    {
        private const string LibraryName = "DiskMgr";

        /*
         * Not implemented yet ... 
         *
        [LibraryImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DiskMgr_CleanDisk(int diskNumber);

        [LibraryImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DiskMgr_InitializeGPT(int diskNumber);

        [LibraryImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DiskMgr_CreateGPTPartition(int diskNumber, ulong sizeInMB, Guid partitionType);

        [LibraryImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DiskMgr_SetGPTPartitionAttributes(int diskNumber, int partitionIndex, ulong attributes);

        [LibraryImport(LibraryName)]
        private static partial ulong DiskMgr_GetDiskSize(int diskNumber);

        [LibraryImport(LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DiskMgr_ChangeGPTPartitionType(int diskNumber, int partitionIndex, Guid newPartitionType);

        [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf16)]
        private static partial int DiskMgr_FormatPartition(
            char driveLetter,
            string fileSystem,
            string label,
            [MarshalAs(UnmanagedType.Bool)] bool quickFormat);
        */

        [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf16)]
        public static partial int DiskMgr_CreateVssSnapshot(
            string volumePath,
            char[] outSnapshotId,
            int snapshotIdBufferSize,
            char[] outDeviceObjectPath,
            int deviceObjectPathBufferSize);

        [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf16)]
        public static partial int DiskMgr_DeleteVssSnapshot(string snapshotId);

        [LibraryImport(LibraryName)]
        public static partial void DiskMgr_SetSilentMode([MarshalAs(UnmanagedType.U1)] bool silent);

        // GPT Partition Type GUIDs
        /*
        private static readonly Guid GUID_EFI_SYSTEM = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
        private static readonly Guid GUID_MSR = new("e3c9e316-0b5c-4db8-817d-f92df00215ae");
        private static readonly Guid GUID_BASIC_DATA = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");
        private static readonly Guid GUID_RECOVERY = new("de94bba4-06d1-4d40-a16a-bfd50179d6ac");

        private const ulong GPT_ATTR_NO_AUTO_MOUNT = 0x8000000000000000;
        private const ulong GPT_ATTR_REQUIRED_PARTITION = 0x0000000000000001;
        */
    }
}
