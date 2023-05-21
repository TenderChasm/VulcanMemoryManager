using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    public interface IBinRelay
    {
        public VramSpace SupremeSpace { get; }
        public ulong RegionStart { get; }
        public ulong RegionEnd { get; }
        

        public ErrorCodes GetSuitableBlock(uint size, uint alignment, out InnerReprMemoryBlock block);

        public ErrorCodes RegisterBlock(InnerReprMemoryBlock block);
    }
}
