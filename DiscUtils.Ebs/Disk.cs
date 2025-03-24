using Amazon;
using Amazon.Runtime;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;

namespace DiscUtils.Ebs {
    public class Disk : VirtualDisk {

        SparseStream sparseStream;

        public int Size { get; private set; }

        public string SnapshotId { get; private set; }

        public Disk(string snapshotId, string region, AWSCredentials credentials) {

            var ebsMappedStream = new EbsMappedStream(snapshotId, credentials, RegionEndpoint.GetBySystemName(region));

            sparseStream = new BlockCacheStream(new AligningStream(ebsMappedStream, Ownership.Dispose, EbsMappedStream.BlockSize),
                Ownership.Dispose,
                new BlockCacheSettings() {
                    BlockSize = EbsMappedStream.BlockSize,
                    LargeReadSize = EbsMappedStream.BlockSize * 10,
                    OptimumReadSize = EbsMappedStream.BlockSize,
                    ReadCacheSize = 1024 * 1024 * 100
                });

            SnapshotId = snapshotId;
            Size = ebsMappedStream.SnapshotBlocks.Count * BlockSize;
        }

        public override Geometry? Geometry => DiscUtils.Geometry.FromCapacity(sparseStream.Length);

        public override VirtualDiskClass DiskClass => VirtualDiskClass.HardDisk;

        public override long Capacity => sparseStream.Length;

        public override SparseStream Content => sparseStream;

        public override IEnumerable<VirtualDiskLayer> Layers => new List<VirtualDiskLayer> { new DiskLayer(sparseStream) };

        public override VirtualDiskTypeInfo DiskTypeInfo {
            get {
                return new VirtualDiskTypeInfo {
                    Name = "EBS",
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
