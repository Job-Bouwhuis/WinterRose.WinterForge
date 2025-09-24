namespace WinterRose.WinterForgeSerializing.Instructions
{
    /// <summary>
    /// An instruction used by the WinterForge VM
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="args"></param>
    public readonly struct Instruction(OpCode opCode, object[] args)
    {
        /// <summary>
        /// The instruction opcode
        /// </summary>
        public readonly OpCode OpCode => opCode;

        /// <summary>
        /// The arguments of the opcode, if any
        /// </summary>
        public readonly object[] Args => args;
    }
}
