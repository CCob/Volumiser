using DiscUtils.Streams;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DiscUtils.RawDisk {
    public class Disk : VirtualDisk {

        SafeFileHandle diskHandle;
        Geometry geometry;

        [Flags]
        public enum EMethod : uint {
            Buffered = 0,
            InDirect = 1,
            OutDirect = 2,
            Neither = 3
        }

        [Flags]
        public enum EFileDevice : uint {
            Disk = 0x00000007,
        }

        [Flags]
        public enum EIOControlCode : uint {
            DiskGetDriveGeometryEx = (EFileDevice.Disk << 16) | (0x0028 << 2) | EMethod.Buffered | (0 << 14),
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetDiskFreeSpace(string lpRootPathName,
            out uint lpSectorsPerCluster,
            out uint lpBytesPerSector,
            out uint lpNumberOfFreeClusters,
            out uint lpTotalNumberOfClusters);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool DeviceIoControl(IntPtr hDevice, EIOControlCode dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);


        [StructLayout(LayoutKind.Sequential)]
        internal class DISK_GEOMETRY {
            public long Cylinders;
            public uint MediaType;

            public uint TracksPerCylinder;
            public uint SectorsPerTrack;
            public uint BytesPerSector;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class DISK_GEOMETRY_EX {
            public DISK_GEOMETRY Geometry;
            public ulong DiskSize;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 1024)]
            public byte[] Data;
        }

        SparseStream sparseStream;

        public Disk(string path) {

            diskHandle = CreateFile(path, FileAccess.Read, FileShare.Read | FileShare.Write, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);

            if (diskHandle.IsInvalid) {
                throw new IOException($"Failed to open raw disk with path {path}");
            }

            var nativePtr = Marshal.AllocHGlobal(Marshal.SizeOf<DISK_GEOMETRY_EX>());

            if(!DeviceIoControl(diskHandle.DangerousGetHandle(), EIOControlCode.DiskGetDriveGeometryEx, IntPtr.Zero, 0, nativePtr , (uint)Marshal.SizeOf<DISK_GEOMETRY_EX>(), out uint bytesReturned, IntPtr.Zero)) {
                throw new IOException($"Failed to query disk geomrtry");
            }

            var nativeGeometry = Marshal.PtrToStructure<DISK_GEOMETRY_EX>(nativePtr);
            Marshal.FreeHGlobal(nativePtr);
            geometry = new Geometry((int)nativeGeometry.Geometry.Cylinders, (int)nativeGeometry.Geometry.TracksPerCylinder, (int)nativeGeometry.Geometry.SectorsPerTrack);

            sparseStream = new BlockCacheStream(new AligningStream(new RawDiskStream(diskHandle, geometry, (long)nativeGeometry.DiskSize), Ownership.Dispose, geometry.BytesPerSector),
               Ownership.Dispose,
               new BlockCacheSettings() {
                   BlockSize = geometry.BytesPerSector,
                   LargeReadSize = 1024 * 1024,
                   OptimumReadSize = 64 * 1024,
                   ReadCacheSize = 1024 * 1024 * 100
               });
        }


        public override Geometry? Geometry => geometry;

        public override VirtualDiskClass DiskClass => VirtualDiskClass.HardDisk;

        public override long Capacity => sparseStream.Length;

        public override SparseStream Content => sparseStream;

        public override IEnumerable<VirtualDiskLayer> Layers => throw new NotImplementedException();

        public override VirtualDiskTypeInfo DiskTypeInfo {
            get {
                return new VirtualDiskTypeInfo {
                    Name = "Raw Disk",
                    CanBeHardDisk = true,
                    DeterministicGeometry = true,
                    PreservesBiosGeometry = false,
                    CalcGeometry = c => DiscUtils.Geometry.FromCapacity(c)
                };
            }
        }

        public override bool CanWrite => throw new NotImplementedException();

        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path) {
            throw new NotImplementedException();
        }

        public override VirtualDisk CreateDifferencingDisk(string path, bool useAsync) {
            throw new NotImplementedException();
        }
    }
}
