using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    class IndexBinsRelay : IBinRelay
    {
        public VramSpace SupremeSpace { get; }
        public ulong RegionStart { get; } = 0;
        public ulong RegionEnd { get; }
        uint Granularity { get; }
        protected List<InnerReprMemoryBlock>[] Bins { get; set; }

        const int binInitialCapacity = 3;

        public IndexBinsRelay(VramSpace space, uint treshold)
        {
            SupremeSpace = space;
            Granularity = SupremeSpace.SupremeType.SupremeCommutator.SupremeVramController.Settings.DefaultMinAlignment;
            uint arrayLength = treshold;
            RegionEnd = treshold;
            Bins = new List<InnerReprMemoryBlock>[arrayLength];

            for(int i = 0;i < Bins.Length; i++)
            {
                Bins[i] = new List<InnerReprMemoryBlock>(binInitialCapacity);
            }
        }

        public ErrorCodes GetSuitableBlock(uint size, uint alignment,out InnerReprMemoryBlock block)
        {
            int startSearchIndex = (int)(size / Granularity);

            var err = BinAssistant.LinearSearchAndMark(Bins, size, alignment, startSearchIndex, out block);
            return err;
        }

        public ErrorCodes RegisterBlock(InnerReprMemoryBlock block)
        {
            int binIndex = (int)(block.Size / Granularity);
            Bins[binIndex].Add(block);

            return ErrorCodes.AllIsOk;
        }
    }
}
