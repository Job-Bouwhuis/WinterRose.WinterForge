namespace WinterRose.WinterForgeSerializing.Instructions
{
    public unsafe record struct Instruction(OpCode OpCode, object[] Args);
}
