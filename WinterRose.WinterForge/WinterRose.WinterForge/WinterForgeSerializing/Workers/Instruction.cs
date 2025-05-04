using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    public record Instruction(OpCode OpCode, string[] Args);
}
