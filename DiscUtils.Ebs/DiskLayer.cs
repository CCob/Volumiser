﻿using DiscUtils.Streams;
using System;

namespace DiscUtils.Ebs {
    public class DiskLayer : VirtualDiskLayer {

        SparseStream sparseStream;

        public override long Capacity => sparseStream.Length;

        public override Geometry Geometry => Geometry.FromCapacity(Capacity);

        public override bool IsSparse => true;

        public override bool NeedsParent => false;

        public override FileLocator RelativeFileLocator => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public DiskLayer(SparseStream sparseStream) {
            this.sparseStream = sparseStream;
        }


        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent) {
            throw new NotImplementedException();
        }
    }
}
