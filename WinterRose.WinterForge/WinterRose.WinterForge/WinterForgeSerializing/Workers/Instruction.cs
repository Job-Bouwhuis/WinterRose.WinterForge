using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    public unsafe record struct Instruction(OpCode OpCode, string[] Args);
}
