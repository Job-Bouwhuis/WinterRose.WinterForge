using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinterRose.Reflection;

namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

/// <summary>
/// New human-readable parser pipeline that performs lexing + AST parsing and emits optimized bytecode.
/// </summary>
public sealed class HumanReadableBytecodeParser
{
    /// <summary>
    /// Parses human-readable WinterForge syntax into optimized bytecode.
    /// </summary>
    public void Parse(Stream input, Stream output, bool allowCustomCompilers = true)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        _ = allowCustomCompilers;

        string source;
        using (var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            source = reader.ReadToEnd();

        IReadOnlyList<HumanReadableToken> tokens = HumanReadableLexer.Tokenize(source);
        HumanReadableProgramNode ast = HumanReadableAstParser.Parse(source, tokens);

        HumanReadableAstBytecodeCompiler.Compile(ast, source, output);
    }

    public void Parse(Stream input, Stream output, CompilationOptions options, bool allowCustomCompilers = true)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        _ = allowCustomCompilers;

        string source;
        using (var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            source = reader.ReadToEnd();

        IReadOnlyList<HumanReadableToken> tokens = options.SkipAstConstruction
            ? HumanReadableLexer.TokenizeFast(source.AsSpan())
            : HumanReadableLexer.Tokenize(source);

        HumanReadableProgramNode ast = HumanReadableAstParser.Parse(source, tokens);

        if (options.EnableAggressiveOptimizations)
            HumanReadableAstBytecodeCompiler.Compile(ast, source, output, options);
        else
            HumanReadableAstBytecodeCompiler.Compile(ast, source, output);
    }

    /// <summary>
    /// Builds and returns a text-tree visualization of the AST for the given source stream.
    /// </summary>
    public string VisualizeAst(Stream input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string source = reader.ReadToEnd();
        return VisualizeAst(source);
    }

    /// <summary>
    /// Builds and returns a text-tree visualization of the AST for the given source text.
    /// </summary>
    public string VisualizeAst(string source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        IReadOnlyList<HumanReadableToken> tokens = HumanReadableLexer.Tokenize(source);
        HumanReadableProgramNode ast = HumanReadableAstParser.Parse(source, tokens);
        return HumanReadableAstVisualizer.Visualize(ast, source);
    }
}
