using System;
using System.Collections.Generic;
using System.Text;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing;

public readonly struct WinterForgeStreamInfo
{
    public readonly int TotalInstructionCount;
    public readonly long RawByteCount;
    public readonly long CompressedByteCount;
    public readonly IReadOnlyDictionary<OpCode, int> InstructionHistogram;

    public WinterForgeStreamInfo(
        int totalInstructionCount,
        long rawByteCount,
        long compressedByteCount,
        IReadOnlyDictionary<OpCode, int> instructionHistogram)
    {
        TotalInstructionCount = totalInstructionCount;
        RawByteCount = rawByteCount;
        CompressedByteCount = compressedByteCount;
        InstructionHistogram = instructionHistogram;
    }

    public override string ToString()
    {
        StringBuilder builder = new();

        builder.AppendLine("");
        builder.AppendLine("------WinterForge Inspection------");
        builder.Append("Total instructions: ")
               .AppendLine(TotalInstructionCount.ToString());

        builder.Append("Raw bytes: ")
               .Append(RawByteCount)
               .AppendLine(" B");

        if (CompressedByteCount > 0)
        {
            builder.Append("Compressed bytes: ")
                   .Append(CompressedByteCount)
                   .AppendLine(" B");

            if (RawByteCount > 0)
            {
                double ratio = (double)CompressedByteCount / RawByteCount;
                builder.Append("Compression ratio: ")
                       .Append(ratio.ToString("0.000"))
                       .AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine("Instruction histogram:");

        foreach (var pair in InstructionHistogram.OrderByDescending(p => p.Value))
        {
            builder.Append("  ")
                   .Append(pair.Key)
                   .Append(": ")
                   .AppendLine(pair.Value.ToString());
        }


        builder.AppendLine("---------------end---------------");
        return builder.ToString();
    }
}
