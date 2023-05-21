using System;
using System.Collections.Generic;
using Engine.Binding.Vulkan;
using Engine.Binding.Vulkan.Native;
using Engine.Graphics.Gapi.Vulkan;
using Engine.Graphics.Vulkan.MemoryManager;
using Engine.Util;

namespace Engine.Graphics.Vulkan.VramController
{
    public class ContrHeap
    {
        public uint Index { get; set; }
        public ulong TotalSize { get; set; }
        public MemoryHeapFlags Flags { get; set; }
    }

    public class Commutator
    {
        public VramController SupremeVramController { get; set; }
        public ContrMemoryType[] AvailableMemTypes { get; set; }
        public ContrHeap[] AvailableHeaps { get; set; }
        public List<ContrMemoryType> SortedAvailableMemTypes { get; set; }
        public List<VramSpace> AllSpaces { get; set; }
        public MemoryBlockDatabase ExistingBlocks { get; set; }



        public Commutator(PhysicalDevice device, VramController supremeVramController)
        {
            SupremeVramController = supremeVramController;
            ExistingBlocks = new MemoryBlockDatabase();
            AllSpaces = new List<VramSpace>();
            InvestigateTypesAndHeaps(device);
            SortMemTypes();
            
        }

        private void InvestigateTypesAndHeaps(PhysicalDevice device)
        {
            var memTypes = ManagedVulkanInstance.Vkh.GetPhysicalDeviceMemoryProperties(device);

            var rawMemoryTypeInfos = memTypes.MemoryTypes;
            var rawMemoryHeapInfos = memTypes.MemoryHeaps;

            AvailableHeaps = new ContrHeap[memTypes.MemoryHeapCount];
            
            for (uint i = 0; i < AvailableHeaps.Length; i++)
            {
                AvailableHeaps[i] = new ContrHeap
                {
                    Index = i,
                    TotalSize = rawMemoryHeapInfos[i].Size,
                    Flags = rawMemoryHeapInfos[i].Flags
                };
            }

            AvailableMemTypes = new ContrMemoryType[memTypes.MemoryTypeCount];
            
            for (uint i = 0; i < memTypes.MemoryTypeCount; i++)
            {
                AvailableMemTypes[i] = new ContrMemoryType
                (
                    i,
                    AvailableHeaps[rawMemoryTypeInfos[i].HeapIndex],
                    rawMemoryTypeInfos[i].PropertyFlags,
                    this
                );
                
            }
            
        }

        private void SortMemTypes()
        {
            int CompareMemFlags(ContrMemoryType a, ContrMemoryType b)
            {
                uint activeBitsA = MathHelper.PopCount((uint)a.Flags);
                uint activeBitsB = MathHelper.PopCount((uint)b.Flags);

                if (activeBitsA > activeBitsB)
                    return 1;
                else if (activeBitsA < activeBitsB)
                    return -1;
                else
                    return 0;

            }
            
            SortedAvailableMemTypes = new List<ContrMemoryType>(AvailableMemTypes);
            SortedAvailableMemTypes.Sort(CompareMemFlags);
        }


        public ErrorCodes ChooseTypeAndAllocate(MemoryChunkRecord chunk)
        {
            var suitableTypes = FindSuitableTypes(chunk);
            ErrorCodes finalErrorCode;
            if (suitableTypes.Count == 0)
                return ErrorCodes.NoSuchType;

            foreach (var type in suitableTypes)
            {
                finalErrorCode = type.Allocate(chunk);
                if (finalErrorCode == ErrorCodes.AllIsOk)
                    return ErrorCodes.AllIsOk;
            }

            foreach (var type in suitableTypes)
            {
                finalErrorCode = type.AddVramSpace();
                if(finalErrorCode == ErrorCodes.AllIsOk)
                {
                    type.Allocate(chunk);
                    return ErrorCodes.AllIsOk;
                }
            }

            return ErrorCodes.DeviceRanOutOfMemory;
        }

        public ErrorCodes Free(ulong[] indexes)
        {
            bool errorDetected = false;
            bool successDetected = false;

            foreach(ulong index in indexes)
            {
                MemoryBlockDatabase.DbRecord obtainedRecord;
                var code = ExistingBlocks.Get(index, out obtainedRecord);

                if (code != ErrorCodes.AllIsOk)
                    errorDetected = true;

                code = obtainedRecord.Space.Free(index);
                if (code != ErrorCodes.AllIsOk)
                    errorDetected = true;
                else
                    successDetected = true;
            }

            if (successDetected && errorDetected)
                return ErrorCodes.RequestCompletedPartially;
            if (!successDetected && errorDetected)
                return ErrorCodes.BlockNotFound;

            return ErrorCodes.AllIsOk;
        }


        public List<ContrMemoryType> FindSuitableTypes(MemoryChunkRecord chunk)
        {
            List<ContrMemoryType> suitableTypes = new();
            foreach (var memType in SortedAvailableMemTypes)
            {
                bool somethingWasNotFound = false;
                var chunkType = chunk.MemoryTypeFlags;
                var memTypeBeingObserved = memType.Flags;
                
                for (int iBit = 0; iBit < sizeof(MemoryPropertyFlags) * 8; iBit++)
                {
                    var chunkTypeBit = chunkType & (MemoryPropertyFlags)1;
                    var currentMemTypeBit = memTypeBeingObserved & (MemoryPropertyFlags)1;

                    if (chunkTypeBit == (MemoryPropertyFlags)1)
                    {
                        if (chunkTypeBit != currentMemTypeBit)
                        {
                            somethingWasNotFound = true;
                            break;
                        }
                        
                    }

                    chunkType = (MemoryPropertyFlags)((int)chunkType >> 1);
                    memTypeBeingObserved = (MemoryPropertyFlags)((int)memTypeBeingObserved >> 1);
                }

                if (somethingWasNotFound == false)
                    suitableTypes.Add(memType);

            }

            if (suitableTypes.Count == 0)
                throw new NotSupportedException("Allocator couldn't find suitable memory type");
            else
                return suitableTypes;
        }

    }
}