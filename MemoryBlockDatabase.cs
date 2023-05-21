using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Graphics.Vulkan.VramController
{
    public class MemoryBlockDatabase
    {
        public class DbRecord
        {
            public VramSpace Space;
            public InnerReprMemoryBlock Block;
            public ulong Previous;
            public ulong Next;
        }
        public ulong OccupiedSize { get; private set; }
        protected ulong FreeTopIndex { get; set; }
        protected const ulong EmptyIndex = 0;

        public Dictionary<ulong, DbRecord> Data { get; }

        public ErrorCodes Get(ulong index,out DbRecord record)
        {
            record = new DbRecord();
            if (!Data.ContainsKey(index))
                return ErrorCodes.BlockNotFound;

            record = Data[index];
            return ErrorCodes.AllIsOk;
        }

        public ErrorCodes Add(DbRecord record, out ulong index)
        {
            index = FreeTopIndex;
            FreeTopIndex++;

            Data.Add(index, record);
            OccupiedSize += record.Block.Size;
            return ErrorCodes.AllIsOk;
        }

        public ErrorCodes Redact(ulong index, DbRecord newRecord)
        {
            if (!Data.ContainsKey(index))
                return ErrorCodes.BlockNotFound;

            Data[index] = newRecord;
            return ErrorCodes.AllIsOk;
        }

        public ErrorCodes Remove(ulong index)
        {
            DbRecord toBeDeleted;
            var res = Get(index, out toBeDeleted);
            if (res == ErrorCodes.AllIsOk)
            {
                OccupiedSize -= toBeDeleted.Block.Size;
                Data.Remove(index);
                return ErrorCodes.AllIsOk;
            }
            else
            {
                return ErrorCodes.BlockNotFound;
            }
        }

        public MemoryBlockDatabase()
        {
            OccupiedSize = 0;
            FreeTopIndex = 1;
            Data = new Dictionary<ulong, DbRecord>();
        }
    }
}
