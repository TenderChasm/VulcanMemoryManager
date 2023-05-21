using Engine.Binding.Vulkan;
using Engine.Binding.Vulkan.Native;
using System;
using System.Collections.Generic;

namespace Engine.Graphics.Vulkan.VramController
{
    public class InnerReprMemoryBlock
    {
        public uint Size;
        public uint Alignment;
        public ulong Address;
        public ulong Id;
        public BlockState State;
        public IntPtr MappedMemory;
    }

    [Flags]
    public enum BlockState : byte
    {
        IsUsed = 1 << 0,
        IsAlive = 1 << 1
    }

    public unsafe class VramSpace
    {
        public const int StandartRelaysCount = 4;
        public enum StandartRelays : int
        {
            LesserIndex,
            SmallRanged,
            MediumRanged,
            MajorExp
        }

        public ContrMemoryType SupremeType { get; set; }
        public DeviceMemory MemoryHandler { get; set; }
        public IntPtr MappedMemory { get; set; }

        public ulong Size { get; set; }

        public IBinRelay[] Relays { get; set; }

        LinkedList<InnerReprMemoryBlock> MainSpace { get; set; }

        public VramSpace(ContrMemoryType type,out ErrorCodes errCode)
        {
            SupremeType = type;
            MainSpace = new LinkedList<InnerReprMemoryBlock>();

            InitInfrastructure();
            errCode = ObtainMemoryFromDriver();
        }

        private ErrorCodes ObtainMemoryFromDriver()
        {
            var device = SupremeType.SupremeCommutator.SupremeVramController.Device;
            Size = SupremeType.AttachedVramSpacesInitialSize;

            try
            {
                MemoryHandler = device.Vkh.AllocateMemory(new MemoryAllocateInfo
                {
                    SType = StructureType.MemoryAllocateInfo,
                    AllocationSize = Size,
                    MemoryTypeIndex = SupremeType.Index
                });
            }
            catch (ApplicationException)
            {
                return ErrorCodes.DeviceRanOutOfMemory;
            }

            if (SupremeType.DoesSupportMapping)
                MappedMemory = device.Vkh.MapMemory(MemoryHandler, 0, Size, 0);

            var motherBlock = new InnerReprMemoryBlock
            {
                Address = 0,
                Size = (uint)Size,
                MappedMemory = MappedMemory,
                State = BlockState.IsAlive
            };
            var newDBrecord = new MemoryBlockDatabase.DbRecord { Block = motherBlock, Space = this };

            ulong newIndex;
            SupremeType.SupremeCommutator.ExistingBlocks.Add(newDBrecord, out newIndex);

            MemoryBlockDatabase.DbRecord motherDbRecordUpdated;
            SupremeType.SupremeCommutator.ExistingBlocks.Get(newIndex, out motherDbRecordUpdated);
            motherDbRecordUpdated.Block.Id = newIndex;
            SupremeType.SupremeCommutator.ExistingBlocks.Redact(newIndex, motherDbRecordUpdated);

            PutInSuitableRelay(motherBlock);

            return ErrorCodes.AllIsOk;
        }

        public void InitInfrastructure()
        {
            var options = SupremeType.SupremeCommutator.SupremeVramController.Settings;
            Relays = new IBinRelay[StandartRelaysCount];

            Relays[(int)StandartRelays.LesserIndex] = new IndexBinsRelay(this, options.LesserIndexBinsThreshold);

            Relays[(int)StandartRelays.SmallRanged] = new RangedBinsRelay(this, options.LesserIndexBinsThreshold,
                options.SmallRangedBinsThreshold, options.SmallRangedBinsGranularity);

            Relays[(int)StandartRelays.MediumRanged] = new RangedBinsRelay(this, options.SmallRangedBinsThreshold,
                options.MediumRangedBinsThreshold, options.MediumRangedBinsGranularity);

            Relays[(int)StandartRelays.MajorExp] = new ExpBinsRelay(this, options.MediumRangedBinsThreshold,
                SupremeType.AttachedVramSpacesInitialSize + 1, options.MajorExpBinsInitialSize);
        }

        public ErrorCodes Allocate(MemoryChunkRecord chunk)
        {
            bool allAreDone = true;

            for(int i = 0; i < chunk.AttachedBlocks.Length; i++)
            {
                var outerBlock = chunk.AttachedBlocks[i];

                if (outerBlock.IsReady == false)
                {
                    ulong index;
                    var code = AllocateBlock(outerBlock, out index);

                    if (code == ErrorCodes.AllIsOk)
                    {

                        MemoryBlockDatabase.DbRecord record;
                        code = SupremeType.SupremeCommutator.ExistingBlocks.Get(index, out record);
                        var appropriateInnerBlock = record.Block;

                        outerBlock.Address = appropriateInnerBlock.Address;
                        outerBlock.DevMem = MemoryHandler;
                        outerBlock.MappedMemory = appropriateInnerBlock.MappedMemory;
                        outerBlock.IsReady = true;
                        outerBlock.Index = appropriateInnerBlock.Id;
                        

                    }
                    else
                    {
                        allAreDone = false;
                    }
                }
            }

            if(allAreDone == false)
            {
                return ErrorCodes.RequestCompletedPartially;
            }
            else
            {
                return ErrorCodes.AllIsOk;
            }
        }

        private ErrorCodes AllocateBlock(OuterReprMemoryBlock requestedBlock,out ulong DbIndex)
        {
            DbIndex = 0;
            int startRelayIndex = 0;
            for(int i = 0; i < Relays.Length; i++)
                if (requestedBlock.Size >= Relays[i].RegionStart && requestedBlock.Size < Relays[i].RegionEnd)
                    startRelayIndex = i;

            bool blockFound = false;
            for(int i = startRelayIndex; i < Relays.Length; i++)
            {
                InnerReprMemoryBlock foundBlock;
                var code = Relays[i].GetSuitableBlock((uint)requestedBlock.Size, (uint)requestedBlock.Alignment, out foundBlock);
                if(code == ErrorCodes.AllIsOk)
                {
                    if (requestedBlock.Size == foundBlock.Size)
                    { 
                        DbIndex = foundBlock.Id;
                        return ErrorCodes.AllIsOk;
                    }
                    else
                    {
                        InnerReprMemoryBlock startBlock, croppedBlock, endBlock;
                        code = SplitBlock(foundBlock, requestedBlock.Size,
                                requestedBlock.Alignment, out croppedBlock, out startBlock, out endBlock);

                        if(code == ErrorCodes.AllIsOk)
                        {
                            DbIndex = croppedBlock.Id;
                            croppedBlock.State |= BlockState.IsUsed;
                            if (startBlock != null)
                                PutInSuitableRelay(startBlock);

                            PutInSuitableRelay(croppedBlock);
                            croppedBlock.State |= BlockState.IsUsed;

                            PutInSuitableRelay(endBlock);
                        }

                    }
                }
            }

            return ErrorCodes.AllIsOk;
        }

        private ErrorCodes PutInSuitableRelay(InnerReprMemoryBlock block)
        {
            foreach (var relay in Relays)
            {
                if (block.Size >= relay.RegionStart && block.Size < relay.RegionEnd)
                {
                    relay.RegisterBlock(block);
                    return ErrorCodes.AllIsOk;
                }
            }

            return ErrorCodes.BlockNotFound;
        }

        private ErrorCodes SplitBlock(InnerReprMemoryBlock blockToSplit, ulong sizeToCut, ulong alignToCut,
            out InnerReprMemoryBlock requested, out InnerReprMemoryBlock startRemainder, out InnerReprMemoryBlock endRemainder)
        {
            var blockDb = SupremeType.SupremeCommutator.ExistingBlocks;

            MemoryBlockDatabase.DbRecord blockToSplitDbRecord;
            blockDb.Get(blockToSplit.Id, out blockToSplitDbRecord);

            blockToSplit.State &= ~BlockState.IsAlive;
            SupremeType.SupremeCommutator.ExistingBlocks.Remove(blockToSplit.Id);

            ulong calculatedAppropriateAdress = blockToSplit.Address / alignToCut * alignToCut;
            if (blockToSplit.Address % alignToCut != 0)
                calculatedAppropriateAdress += alignToCut;

            uint startRemainderSize = (uint)(calculatedAppropriateAdress - blockToSplit.Address);
            uint endRemainderSize = (uint)((blockToSplit.Address + blockToSplit.Size) - (calculatedAppropriateAdress + sizeToCut));

            var requestedBlank = new InnerReprMemoryBlock
            {
                Size = (uint) sizeToCut,
                Address = calculatedAppropriateAdress,
                MappedMemory = blockToSplit.MappedMemory + (int)startRemainderSize,
                Alignment = (uint)alignToCut,
            };
            requestedBlank.State |= BlockState.IsAlive;

            var endRemainderBlank = new InnerReprMemoryBlock
            {
                Size = endRemainderSize,
                Address = calculatedAppropriateAdress + sizeToCut,
                MappedMemory = blockToSplit.MappedMemory + (int)startRemainderSize + (int)sizeToCut,
                Alignment = SupremeType.SupremeCommutator.SupremeVramController.Settings.DefaultMinAlignment
            };
            endRemainderBlank.State |= BlockState.IsAlive;

            InnerReprMemoryBlock startRemainderBlank;
            if (startRemainderSize != 0)
            {
                startRemainderBlank = new InnerReprMemoryBlock
                {
                    Size = startRemainderSize,
                    Address = blockToSplit.Address,
                    MappedMemory = blockToSplit.MappedMemory,
                    Alignment = SupremeType.SupremeCommutator.SupremeVramController.Settings.DefaultMinAlignment
                };
                startRemainderBlank.State |= BlockState.IsAlive;
            }
            else
            {
               startRemainderBlank = null;
            }

            ulong requestedBlankId, endRemainderBlankId, startRemainderBlankId = 0;

            SupremeType.SupremeCommutator.ExistingBlocks.Add(
                    new MemoryBlockDatabase.DbRecord { Block = requestedBlank, Space = this }, out requestedBlankId);
            SupremeType.SupremeCommutator.ExistingBlocks.Add(
                   new MemoryBlockDatabase.DbRecord { Block = endRemainderBlank, Space = this }, out endRemainderBlankId);
            if (startRemainderBlank != null)
            {
                SupremeType.SupremeCommutator.ExistingBlocks.Add(
                    new MemoryBlockDatabase.DbRecord { Block = startRemainderBlank, Space = this }, out startRemainderBlankId);
            }

            MemoryBlockDatabase.DbRecord requestedDbRecordUpdated;
            blockDb.Get(requestedBlankId, out requestedDbRecordUpdated);
            requestedDbRecordUpdated.Previous = startRemainderBlank == null ? blockToSplitDbRecord.Previous : startRemainderBlankId;
            requestedDbRecordUpdated.Next = endRemainderBlankId;
            requestedDbRecordUpdated.Block.Id = requestedBlankId;
            blockDb.Redact(requestedBlankId, requestedDbRecordUpdated);

            MemoryBlockDatabase.DbRecord endRemainderDbRecordUpdated;
            blockDb.Get(endRemainderBlankId, out endRemainderDbRecordUpdated);
            endRemainderDbRecordUpdated.Previous = requestedBlankId;
            endRemainderDbRecordUpdated.Next = blockToSplitDbRecord.Next;
            endRemainderDbRecordUpdated.Block.Id = endRemainderBlankId;
            blockDb.Redact(endRemainderBlankId, endRemainderDbRecordUpdated);

            if(startRemainderBlank != null)
            {
                MemoryBlockDatabase.DbRecord startRemainderDbRecordUpdated;
                blockDb.Get(startRemainderBlankId, out startRemainderDbRecordUpdated);
                startRemainderDbRecordUpdated.Previous = blockToSplitDbRecord.Previous;
                startRemainderDbRecordUpdated.Next = requestedBlankId;
                startRemainderDbRecordUpdated.Block.Id = startRemainderBlankId;
                blockDb.Redact(startRemainderBlankId, startRemainderDbRecordUpdated);
            }

            if (blockToSplitDbRecord.Previous != 0)
            {
                MemoryBlockDatabase.DbRecord previousDbRecordUpdated;
                SupremeType.SupremeCommutator.ExistingBlocks.Get(blockToSplitDbRecord.Previous, out previousDbRecordUpdated);
                previousDbRecordUpdated.Next = startRemainderBlank == null ? requestedBlankId : startRemainderBlankId;
                SupremeType.SupremeCommutator.ExistingBlocks.Redact(blockToSplitDbRecord.Previous, previousDbRecordUpdated);
            }

            if (blockToSplitDbRecord.Next != 0)
            {
                MemoryBlockDatabase.DbRecord nextDbRecordUpdated;
                SupremeType.SupremeCommutator.ExistingBlocks.Get(blockToSplitDbRecord.Next, out nextDbRecordUpdated);
                nextDbRecordUpdated.Previous = endRemainderBlankId;
                SupremeType.SupremeCommutator.ExistingBlocks.Redact(blockToSplitDbRecord.Next, nextDbRecordUpdated);
            }

            requested = requestedBlank;
            endRemainder = endRemainderBlank;
            startRemainder = startRemainderBlank;

            return ErrorCodes.AllIsOk;
        }

        public ErrorCodes Free(ulong index)
        {
            MemoryBlockDatabase.DbRecord blockRecord;
            var code = SupremeType.SupremeCommutator.ExistingBlocks.Get(index, out blockRecord);

            if (code != ErrorCodes.AllIsOk)
                return ErrorCodes.BlockNotFound;

            bool wasCoalesced = false;
            var coalescedDbRecord = TryCoalesceBlock(blockRecord, out wasCoalesced);

            if(wasCoalesced)
            {
                PutInSuitableRelay(coalescedDbRecord.Block);
            }
            else
            {
                blockRecord.Block.State &= ~BlockState.IsUsed;
            }

            return ErrorCodes.AllIsOk;

        }

        private MemoryBlockDatabase.DbRecord TryCoalesceBlock(MemoryBlockDatabase.DbRecord blockRecord,out bool wasCoalesced)
        {
            ulong size = blockRecord.Block.Size;
            ulong start = blockRecord.Block.Address;
            var mappedMemory = blockRecord.Block.MappedMemory;
            ulong previous = blockRecord.Previous;
            ulong next = blockRecord.Next;

            bool needToBeCoalesced = false;

            var current = blockRecord;
            while (current.Previous != 0)
            {
                var code = SupremeType.SupremeCommutator.ExistingBlocks.Get(current.Previous, out current);
                if (current.Block.State.HasFlag(BlockState.IsUsed))
                    break;

                needToBeCoalesced = true;

                size += current.Block.Size;
                start = current.Block.Address;
                previous = current.Previous;
                mappedMemory = current.Block.MappedMemory;

                DestroyBlock(current.Block);
            }

            current = blockRecord;
            while (current.Next != 0)
            {
                var code = SupremeType.SupremeCommutator.ExistingBlocks.Get(current.Next, out current);
                if (current.Block.State.HasFlag(BlockState.IsUsed))
                    break;

                needToBeCoalesced = true;

                size += current.Block.Size;
                next = current.Next;
                SupremeType.SupremeCommutator.ExistingBlocks.Remove(current.Block.Id);

                DestroyBlock(current.Block);
            }

            if(needToBeCoalesced == false)
            {
                wasCoalesced = false;
                return blockRecord;
            }

            DestroyBlock(blockRecord.Block);

            var newCoalescedBlock = new InnerReprMemoryBlock
            {
                Address = start,
                Size = (uint)size,
                MappedMemory = mappedMemory,
                State = BlockState.IsAlive
            };
            var newDBrecord = new MemoryBlockDatabase.DbRecord
            {
                Block = newCoalescedBlock,
                Space = this,
                Previous = previous,
                Next = next
            };

            ulong newIndex;
            SupremeType.SupremeCommutator.ExistingBlocks.Add(newDBrecord, out newIndex);

            MemoryBlockDatabase.DbRecord coalescedDbRecordUpdated;
            SupremeType.SupremeCommutator.ExistingBlocks.Get(newIndex, out coalescedDbRecordUpdated);
            coalescedDbRecordUpdated.Block.Id = newIndex;
            SupremeType.SupremeCommutator.ExistingBlocks.Redact(newIndex, coalescedDbRecordUpdated);

            if (previous != 0)
            {
                MemoryBlockDatabase.DbRecord previousDbRecordUpdated;
                SupremeType.SupremeCommutator.ExistingBlocks.Get(previous, out previousDbRecordUpdated);
                coalescedDbRecordUpdated.Next = newIndex;
                SupremeType.SupremeCommutator.ExistingBlocks.Redact(previous, previousDbRecordUpdated);
            }

            if (next != 0)
            {
                MemoryBlockDatabase.DbRecord nextDbRecordUpdated;
                SupremeType.SupremeCommutator.ExistingBlocks.Get(next, out nextDbRecordUpdated);
                coalescedDbRecordUpdated.Previous = newIndex;
                SupremeType.SupremeCommutator.ExistingBlocks.Redact(next, nextDbRecordUpdated);
            }



            wasCoalesced = true;
            return coalescedDbRecordUpdated;

        }

        private void DestroyBlock(InnerReprMemoryBlock block)
        {
            block.State &= ~BlockState.IsAlive;
            SupremeType.SupremeCommutator.ExistingBlocks.Remove(block.Id);
        }

    }
}