using Amazon;
using Amazon.EBS;
using Amazon.EBS.Model;
using Amazon.Runtime;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscUtils.Ebs {
    public class EbsMappedStream : SparseStream {

        internal AmazonEBSClient EbsClient { get; private set; }
        long capacity = 0;
        long position;
        
        public string SnapshotId { get; private set; }

        public Dictionary<long, Block> SnapshotBlocks { get; private set; } = new Dictionary<long, Block>();

        public const int BlockSize = 512 * 1024;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => capacity;

        public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }

        public override IEnumerable<StreamExtent> Extents => new List<StreamExtent>();

        public EbsMappedStream(string snapshotId, AWSCredentials credentials, RegionEndpoint region) {

            SnapshotId = snapshotId;
            EbsClient = new AmazonEBSClient(credentials, region);
           
            ListSnapshotBlocksResponse response = null;

            while (response == null || response.NextToken != null) {

                response = EbsClient.ListSnapshotBlocks(new ListSnapshotBlocksRequest {
                    MaxResults = 500,
                    SnapshotId = SnapshotId,
                    StartingBlockIndex = 0,
                    NextToken = response?.NextToken
                });

                if (response.HttpStatusCode != HttpStatusCode.OK)
                    throw new AmazonEBSException($"Failed to query EBS block with error {response.HttpStatusCode}");

                foreach (var block in response.Blocks) {
                    SnapshotBlocks[block.BlockIndex] = new Block(block.BlockIndex, block.BlockToken);
                }
            }

            capacity = response.VolumeSize * 1024 * 1024 * 1024;
        }
  
        public override void Flush() {
   
        }

        public override int Read(byte[] buffer, int offset, int count) {

            if(count % BlockSize != 0) {
                throw new IOException("Read count should be multiple of EBS block size 512K");
            }

            long startBlock = position / BlockSize; 
            long numBlocks = count / BlockSize;
            int currentOffset = offset;

            for (long currentBlock = startBlock; currentBlock < startBlock + numBlocks; currentBlock++) {

                if (SnapshotBlocks.ContainsKey(currentBlock)) {
                    GetBlockData(SnapshotBlocks[currentBlock], buffer, currentOffset);
                } else {
                    Array.Clear(buffer, currentOffset, BlockSize);
                }

                currentOffset += BlockSize;               
            }

            position += count;
            return count;
        }  

        void GetBlockData(Block block, byte[] buffer, int offset) {

            var result = EbsClient.GetSnapshotBlock(new GetSnapshotBlockRequest() {
                BlockIndex = block.Index,
                BlockToken = block.Token,
                SnapshotId = SnapshotId
            });

            if(result.HttpStatusCode != System.Net.HttpStatusCode.OK) {
                throw new IOException($"Failed to read EBS block {block.Index} with HTTP error {result.HttpStatusCode}");
            }

            StreamUtilities.ReadExact(result.BlockData, buffer, offset, BlockSize);
        }
        
        public override long Seek(long offset, SeekOrigin origin) {

            if (offset % BlockSize != 0) {
                throw new IOException("Offset should be multiple of EBS block size 512K");
            }

            switch (origin) {
                case SeekOrigin.Begin:
                    position = Math.Min(Length, offset);
                    break;
                case SeekOrigin.Current:
                    position = Math.Min(Length, position + offset); 
                    break;
                case SeekOrigin.End:                    
                    position = Math.Min(Length, Length + offset);
                    break;
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
