using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    class ExpBinsRelay : IBinRelay
    {
        public VramSpace SupremeSpace { get; }

        public ulong RegionStart { get; }
        public ulong RegionEnd { get; }
        public ulong InitialSize { get; }

        protected List<InnerReprMemoryBlock>[] Bins { get; set; }

        const int binInitialCapacity = 10;
        const int GrowFactor = 2;

        public ExpBinsRelay(VramSpace space, ulong startAdress, ulong tresholdAdress, ulong initialSize)
        {
            RegionStart = startAdress;
            RegionEnd = tresholdAdress;
            InitialSize = initialSize;

            SupremeSpace = space;

            int binsCount = 0;
            ulong currAugmentation = initialSize;
            ulong currAdress = startAdress;
            do
            {
                currAdress += currAugmentation;
                currAugmentation *= GrowFactor;
                binsCount++;
            } while (currAdress < tresholdAdress);

            Bins = new List<InnerReprMemoryBlock>[binsCount];
            for (int i = 0; i < Bins.Length; i++)
                Bins[i] = new List<InnerReprMemoryBlock>(binInitialCapacity);
        }

        private int FindExpAppropriateSizedBin(uint size)
        {
            ulong currAugmentation = InitialSize;
            ulong currAdress = RegionStart;
            int startSearchIndex = 0;
            while (currAdress <= RegionEnd)
            {
                currAdress += currAugmentation;

                if (size < currAdress)
                    break;

                currAugmentation *= GrowFactor;
                startSearchIndex++;
            }

            return startSearchIndex;
        }

        public ErrorCodes GetSuitableBlock(uint size, uint alignment, out InnerReprMemoryBlock block)
        {
            int startSearchIndex = FindExpAppropriateSizedBin(size);
            if (size < RegionStart)
                startSearchIndex = 0;

            var err = BinAssistant.LinearSearchAndMark(Bins, size, alignment, startSearchIndex, out block);
            return err;
        }

        public ErrorCodes RegisterBlock(InnerReprMemoryBlock block)
        {
            int binIndex = FindExpAppropriateSizedBin(block.Size);

            Bins[binIndex].Add(block);
            return ErrorCodes.AllIsOk;
        }


    }
}
