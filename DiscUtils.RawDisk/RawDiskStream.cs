using Amazon;
using Amazon.EBS;
using Amazon.EBS.Model;
using Amazon.Runtime;
using DiscUtils.Streams;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DiscUtils.RawDisk {
    public class RawDiskStream : SparseStream {

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer,
           uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);


        [DllImport("kernel32.dll")]
        static extern bool SetFilePointerEx(IntPtr hFile, long liDistanceToMove,
           out long lpNewFilePointer, SeekOrigin moveMethod);

        internal SafeFileHandle diskHandle;
        long capacity = 0;
        long position;
        int blockSize;

       
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => capacity;

        public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }

        public override IEnumerable<StreamExtent> Extents => new List<StreamExtent>();

        public RawDiskStream(SafeFileHandle diskHandle, Geometry geometry, long diskSize) {
            this.diskHandle = diskHandle;
            blockSize = geometry.BytesPerSector;
            capacity = diskSize;
        }
  
        public override void Flush() {
   
        }

        public override int Read(byte[] buffer, int offset, int count) {

            if(count % blockSize != 0) {
                throw new IOException("Read count should be multiple of EBS block size 512K");
            }

            byte[] tempData = new byte[count];

            if(!ReadFile(diskHandle.DangerousGetHandle(), tempData, (uint)count, out uint bytesRead, IntPtr.Zero)) {
                throw new IOException("Failed to read data from disk");
            }

            Array.Copy(tempData, 0, buffer, offset, bytesRead);      
            position += bytesRead;
            return count;
        }  

        
        public override long Seek(long offset, SeekOrigin origin) {

            if (offset % blockSize != 0) {
                throw new IOException("Offset should be multiple of EBS block size 512K");
            }
     
            if(!SetFilePointerEx(diskHandle.DangerousGetHandle(), offset, out position, origin)) {
                throw new IOException($"Failed to seek to disk offset {offset}");
            }
            
            return position;
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }
    }
}
