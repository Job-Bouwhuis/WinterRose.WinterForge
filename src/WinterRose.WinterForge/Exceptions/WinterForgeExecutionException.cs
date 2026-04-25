
using System.Text;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Workers;

[Serializable]
internal class WinterForgeExecutionException : Exception
{
    public int? InstructionIndex { get; }
    public OpCode? Opcode { get; }
    public string? InstructionText { get; }
    public string? InstructionContext { get; }
    public string? ScopePath { get; }
    public int? ValueStackCount { get; }
    public int? InstanceStackDepth { get; }

    public bool HasExecutionContext => InstructionIndex.HasValue || !string.IsNullOrWhiteSpace(InstructionContext);

    public WinterForgeExecutionException()
    {
    }

    public WinterForgeExecutionException(string? message) : base(message)
    {
    }

    public WinterForgeExecutionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public WinterForgeExecutionException(
        string? message,
        Exception? innerException,
        int? instructionIndex,
        OpCode? opcode,
        string? instructionText,
        string? instructionContext,
        string? scopePath,
        int? valueStackCount,
        int? instanceStackDepth)
        : base(message, innerException)
    {
        InstructionIndex = instructionIndex;
        Opcode = opcode;
        InstructionText = instructionText;
        InstructionContext = instructionContext;
        ScopePath = scopePath;
        ValueStackCount = valueStackCount;
        InstanceStackDepth = instanceStackDepth;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine(base.ToString());

        if (InstructionIndex.HasValue || Opcode.HasValue)
            sb.AppendLine($"Execution context: instruction #{InstructionIndex?.ToString() ?? "?"}, opcode={Opcode?.ToString() ?? "?"}");

        if (!string.IsNullOrWhiteSpace(InstructionText))
            sb.AppendLine($"Instruction: {InstructionText}");

        if (!string.IsNullOrWhiteSpace(ScopePath))
            sb.AppendLine($"Scope path: {ScopePath}");

        if (ValueStackCount.HasValue || InstanceStackDepth.HasValue)
            sb.AppendLine($"Stacks: value={ValueStackCount?.ToString() ?? "?"}, instance={InstanceStackDepth?.ToString() ?? "?"}");

        if (!string.IsNullOrWhiteSpace(InstructionContext))
        {
            sb.AppendLine("Instruction window:");
            sb.AppendLine(InstructionContext);
        }

        return sb.ToString().TrimEnd();
    }
}