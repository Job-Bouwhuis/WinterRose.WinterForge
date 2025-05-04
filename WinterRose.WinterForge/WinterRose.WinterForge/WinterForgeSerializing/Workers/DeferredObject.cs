using System.Collections.Generic;

namespace WinterRose.WinterForgeSerializing.Workers
{
    internal class DeferredObject
    {
        public int TargetId { get; }
        public int ContextId { get; }
        public List<Instruction> Instructions { get; }

        public DeferredObject(int targetId, int contextId)
        {
            TargetId = targetId;
            ContextId = contextId;
            Instructions = new();
        }
    }

}
