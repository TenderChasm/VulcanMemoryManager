using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    class RangedBinsRelay : IBinRelay
    {
        public VramSpace SupremeSpace { get; }

        public ulong RegionStart { get; }
        public ulong RegionEnd { get; }
        public uint Granularity { get; }

        protected List<InnerReprMemoryBlock>[] Bins { get; set; }

        protected const int binInitialCapacity = 5;

        public RangedBinsRelay(VramSpace space, uint startAdress, uint tresholdAdress, uint granularity)
        {
            RegionStart = startAdress;
            RegionEnd = tresholdAdress;
            Granularity = granularity;

            SupremeSpace = space;

            uint binsCount = (tresholdAdress - startAdress) / granularity;
            Bins = new List<InnerReprMemoryBlock>[binsCount];
            for (int i = 0; i < Bins.Length; i++)
                Bins[i] = new List<InnerReprMemoryBlock>(binInitialCapacity);
        }

        public ErrorCodes GetSuitableBlock(uint size, uint alignment, out InnerReprMemoryBlock block)
        {
            int startSearchIndex = (int)((size - RegionStart) / Granularity);
            if ((size - RegionStart) % Granularity == 0)
                startSearchIndex = Math.Max(startSearchIndex - 1, 0);
            if (size < RegionStart)
                startSearchIndex = 0;

            var err = BinAssistant.LinearSearchAndMark(Bins, size, alignment, startSearchIndex, out block);
            return err;
        }

        public ErrorCodes RegisterBlock(InnerReprMemoryBlock block)
        {
            int binIndex = (int)((block.Size - RegionStart) / Granularity);
            if ((block.Size - RegionStart) % Granularity == 0)
                binIndex = Math.Max(binIndex - 1, 0);

            Bins[binIndex].Add(block);

            return ErrorCodes.AllIsOk;
        }
    }
}
