using System;
using Engine.Binding.Vulkan.Native;
using Engine.Graphics.Gapi.Vulkan;
using Engine.Graphics.Vulkan.MemoryManager;

namespace Engine.Graphics.Vulkan.VramController
{
    public class MemoryChunkRecord
    {
        public MemoryPropertyFlags MemoryTypeFlags;
        public ControllerFlags ControllerFlags; 
        public OuterReprMemoryBlock[] AttachedBlocks;
        
        public ulong Size;

    }

    public class OuterReprMemoryBlock
    {
        public ulong Size;
        public ulong Alignment;

        public bool IsReady;
        public ulong Address;
        public IntPtr MappedMemory;
        public ulong Index;
        public DeviceMemory DevMem;
    }

    public enum ControllerFlags
    {
        Movable = 1 << 0
    }

    public enum ErrorCodes
    {
        AllIsOk,
        VramSpaceIsReplete,
        MemoryTypeIsReplete,
        NoSuchType,
        DeviceRanOutOfMemory,
        BlockNotFound,
        BlockAlreadyPresented,
        RequestCompletedPartially
    }


    public class VramController
    {
        public class InitialSettings
        {
            public const uint DefaultVramSpaceSizeForArbitraryMemoryType = 256 << 20;
            public uint LesserIndexBinsThreshold  = 1 << 10;
            public uint SmallRangedBinsThreshold = 256 << 10;
            public uint SmallRangedBinsGranularity = 1 << 10;
            public uint MediumRangedBinsThreshold = 10 << 20;
            public uint MediumRangedBinsGranularity = 16 << 10;
            public uint MajorExpBinsThreshold = DefaultVramSpaceSizeForArbitraryMemoryType;
            public uint MajorExpBinsInitialSize = 1 << 20;
            public uint DefaultMinAlignment = 4;
            public uint DefragmentationStartRequestSizeTreshold = 5 << 10;
        }

        public InitialSettings Settings { get; }
        public VulkanDevice Device { get; set; }
        public PhysicalDevice PhysDevice { get; set; }

        public Commutator TypeCommutator { get; set; }
        


        public VramController(VulkanDevice device)
        {
            Settings = new InitialSettings();
            Device = device;
            PhysDevice = device.PhysicalDevice;
            TypeCommutator = new Commutator(PhysDevice, this);
        }

        public OuterReprMemoryBlock Allocate(ulong size, ulong alighnment, MemoryPropertyFlags flags, ControllerFlags contrFlags = 0)
        {
            var block = new OuterReprMemoryBlock
            {
                Size = size,
                Alignment = alighnment
            };

            var chunk = new MemoryChunkRecord
            {
                MemoryTypeFlags = flags,
                ControllerFlags = contrFlags,
                AttachedBlocks = new[] {block}
            };

            if (!Allocate(chunk))
                throw new ApplicationException("Failed to allocate VRAM");

            return block;
        }

        public bool Allocate(MemoryChunkRecord chunk)
        {
            var result = TypeCommutator.ChooseTypeAndAllocate(chunk);

            return result == ErrorCodes.AllIsOk;
        }

        public ErrorCodes Free(ulong[] indexes)
        {
            return TypeCommutator.Free(indexes);
        }


    }
}