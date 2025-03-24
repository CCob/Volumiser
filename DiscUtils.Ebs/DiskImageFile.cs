using Amazon;
using Amazon.EBS;
using Amazon.EBS.Model;
using Amazon.Runtime;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.Net;

namespace DiscUtils.Ebs {


    public class DiskImageFile : VirtualDiskLayer {

        internal AmazonEBSClient EbsClient { get; private set; }
        long capacity = 0;
        Geometry geometry;

        public string SnapshotId { get; private set; }

        public DiskImageFile(string snapshotId) : this(snapshotId, null) {
        }

        public DiskImageFile(string snapshotId, AWSCredentials credentials) : this(snapshotId, credentials, null) {
        }

        public DiskImageFile(string snapshotId, AWSCredentials credentials, RegionEndpoint region) {

            SnapshotId = snapshotId;
            EbsClient = new AmazonEBSClient(credentials.GetCredentials().AccessKey, credentials.GetCredentials().SecretKey, region);

            var result = EbsClient.ListSnapshotBlocks(new ListSnapshotBlocksRequest() {
                MaxResults = 1,
                SnapshotId = snapshotId
            });

            if (result.HttpStatusCode != HttpStatusCode.OK)
                throw new AmazonEBSException($"Failed to query EBS block with error {result.HttpStatusCode}");

            capacity = result.VolumeSize * 1024 * 1024 * 1024;
            geometry = Geometry.FromCapacity(capacity);
        }

        public override long Capacity => capacity;

        public override Geometry Geometry => geometry;

        public override bool IsSparse => true;

        public override bool NeedsParent => false;

        public override FileLocator RelativeFileLocator => throw new NotImplementedException();

        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent) {

            SparseStream theParent = parent;
            Ownership theOwnership = ownsParent;

            if (parent == null) {
                theParent = new ZeroStream(Capacity);
                theOwnership = Ownership.Dispose;
            }

            // EbsMappedStream contentStream = new EbsMappedStream((DiskExtent)Extents[0]);            
            //return new AligningStream(contentStream, Ownership.Dispose, 512 * 1024);
            return null;
        }

        public override IList<VirtualDiskExtent> Extents { get {
            List<VirtualDiskExtent> result = new List<VirtualDiskExtent>();
                result.Add(new DiskExtent(this));
                return result;                              
            } 
        }

        public override bool CanWrite => throw new NotImplementedException();
    }
}
