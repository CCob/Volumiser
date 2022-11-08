using Amazon.EBS.Model;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscUtils.Ebs {
    class DiskExtent : VirtualDiskExtent {

        public Dictionary<long, Block> SnapshotBlocks { get; private set; }

        public DiskImageFile EbsDisk { get; private set; }

        long capacity;

        public DiskExtent(DiskImageFile ebsDisk) {
            EbsDisk = ebsDisk;

            ListSnapshotBlocksResponse response = null;

            while (response == null || response.NextToken != null) {

                response = EbsDisk.EbsClient.ListSnapshotBlocks(new ListSnapshotBlocksRequest {
                    MaxResults = 500,
                    SnapshotId = EbsDisk.SnapshotId,
                    NextToken = response?.NextToken
                });
                
                foreach(var block in response.Blocks) {
                    SnapshotBlocks[block.BlockIndex] = new Block(block.BlockIndex, block.BlockToken);
                }         
            }

            capacity = SnapshotBlocks.Count * 512 * 1024;
        }

        public override long Capacity => EbsDisk.Capacity;

        public override bool IsSparse => EbsDisk.IsSparse;

        public override long StoredSize => capacity;

        public override MappedStream OpenContent(SparseStream parent, Ownership ownsParent) {
            throw new NotImplementedException();                                  
        }




    }

}
