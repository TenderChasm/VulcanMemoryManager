using Engine.Binding.Vulkan.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    public class ContrMemoryType
    {
        public Commutator SupremeCommutator { get; set; }
        public uint Index { get; set; }
        public ContrHeap AttachedHeap { get; set; }
        public List<VramSpace> AttachedVramSpaces { get; set; }
        public MemoryPropertyFlags Flags { get; set; }

        public ulong AttachedVramSpacesInitialSize;

        public bool DoesSupportMapping
        {
            get { return Flags.HasFlag(MemoryPropertyFlags.HostVisibleBit); }
        }

        public ContrMemoryType(uint index, ContrHeap attachedHeap, MemoryPropertyFlags flags, Commutator supremeCommutator)
        {
            SupremeCommutator = supremeCommutator;

            Index = index;
            AttachedHeap = attachedHeap;
            AttachedVramSpaces = new List<VramSpace>();
            Flags = flags;

            AttachedVramSpacesInitialSize = Math.Min(AttachedHeap.TotalSize / 8,
                VramController.InitialSettings.DefaultVramSpaceSizeForArbitraryMemoryType);
        }

        public ErrorCodes AddVramSpace()
        {
            ErrorCodes code;
            var space = new VramSpace(this, out code);

            if (code == ErrorCodes.AllIsOk)
                AttachedVramSpaces.Add(space);

            return code;
        }

        public ErrorCodes Allocate(MemoryChunkRecord chunk)
        {
            foreach(var space in AttachedVramSpaces)
            {
                var res = space.Allocate(chunk);
                if (res == ErrorCodes.AllIsOk)
                    return ErrorCodes.AllIsOk;
            }

            return ErrorCodes.MemoryTypeIsReplete;
        }

    }
}
