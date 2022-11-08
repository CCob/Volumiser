using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscUtils.Ebs {
    public class Block {
        public int Index { get; private set; } 
        public string Token { get; private set; }
        public Block(int index, string token) {
            Index = index;
            Token = token;
        }
    }
}
