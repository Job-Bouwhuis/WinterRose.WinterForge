namespace WinterRose.WinterForgeSerializing;

/// <summary>
/// 
/// </summary>
public readonly ref struct ProgressMark
{
    public ProgressMark(int currentInstruction, int instructionCount)
    {
        CurrentInstruction = currentInstruction;
        InstructionCount = instructionCount;
    }

    /// <summary>
    /// The current instruction the deserializer is on
    /// </summary>
    public int CurrentInstruction { get; }

    /// <summary>
    /// The total amount of instructions the deserializer has to work through
    /// </summary>
    public int InstructionCount { get; }

    public float ProgressFloat => (float)CurrentInstruction / InstructionCount;
}