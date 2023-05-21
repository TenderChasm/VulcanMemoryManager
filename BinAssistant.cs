using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    static class BinAssistant
    {
        public static ErrorCodes LinearSearchAndMark(List<InnerReprMemoryBlock>[] bins, uint size, uint alignment,
            int startSearchIndex, out InnerReprMemoryBlock block, bool searchDirection = false)
        {

            void CleanBin(List<InnerReprMemoryBlock> bin, List<int> indexes)
            {
                for (int i = indexes.Count - 1; i >= 0; i--)
                {
                    var ind = indexes[i];
                    bin.RemoveAt((int)ind);
                }
            }

            var indexesToRemove = new List<int>();
            for (int binIndex = startSearchIndex; binIndex < bins.Length; binIndex++)
            {
                indexesToRemove.Clear();
                var bin = bins[binIndex];

                for (int memBlockIndex = 0; memBlockIndex < bin.Count; memBlockIndex++)
                {
                    if (bin[memBlockIndex].State.HasFlag(BlockState.IsAlive) && !bin[memBlockIndex].State.HasFlag(BlockState.IsUsed))
                    {
                        ulong neededStart = bin[memBlockIndex].Address / alignment * alignment;
                        if (bin[memBlockIndex].Address % alignment != 0)
                            neededStart += alignment;
                        ulong neededEnd = neededStart + size; 

                        if (neededStart >= bin[memBlockIndex].Address &&
                            neededEnd <= bin[memBlockIndex].Address + bin[memBlockIndex].Size)
                        {
                            block = bin[memBlockIndex];
                            CleanBin(bin, indexesToRemove);

                            return ErrorCodes.AllIsOk;
                        }
                    }
                    else
                    {
                        indexesToRemove.Add(memBlockIndex);
                    }

                }

                CleanBin(bin, indexesToRemove);
            }

            block = null;
            return ErrorCodes.BlockNotFound;
        }
    }
}
