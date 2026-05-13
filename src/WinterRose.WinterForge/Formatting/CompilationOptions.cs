namespace WinterRose.WinterForgeSerializing.Formatting;

/// <summary>
/// Options to control the compilation pipeline and enable optimization shortcuts.
/// Priority is speed, with memory usage as secondary concern if it leads to speed improvements.
/// </summary>
public class CompilationOptions
{
    /// <summary>
    /// The default compilation options used when none are provided.
    /// Can be overridden to customize global defaults.
    /// </summary>
    public static CompilationOptions Default { get; set; } = FastestPath();

    /// <summary>
    /// The target output format for serialization.
    /// </summary>
    public TargetFormat TargetFormat { get; set; } = TargetFormat.Optimized;

    /// <summary>
    /// When true, skips emitting human-readable intermediate representation and directly emits binary opcodes.
    /// This is the fastest path but prevents access to the human-readable format.
    /// Default: true (fastest)
    /// </summary>
    public bool DirectBinaryEmit { get; set; } = true;

    /// <summary>
    /// When true, skips the AST parsing stage and uses a simplified direct compilation approach.
    /// Only available when DirectBinaryEmit is true.
    /// Default: true (for maximum speed)
    /// </summary>
    public bool SkipAstConstruction { get; set; } = false;

    /// <summary>
    /// When true, enables various micro-optimizations at the cost of slightly more memory usage.
    /// Default: true (speed is priority)
    /// </summary>
    public bool EnableAggressiveOptimizations { get; set; } = true;

    /// <summary>
    /// When true, caches frequently accessed type information to speed up serialization.
    /// Default: true
    /// </summary>
    public bool EnableTypeCache { get; set; } = true;

    /// <summary>
    /// Merge this compilation options with another, preferring this instance's values for non-null properties.
    /// </summary>
    public static CompilationOptions Merge(CompilationOptions? primary, CompilationOptions? secondary)
    {
        return primary ?? secondary ?? Default;
    }

    /// <summary>
    /// Creates a compilation options preset optimized for maximum speed with no intermediate representations.
    /// </summary>
    public static CompilationOptions FastestPath() => new()
    {
        DirectBinaryEmit = true,
        SkipAstConstruction = true,
        EnableAggressiveOptimizations = true,
        EnableTypeCache = true
    };

    /// <summary>
    /// Creates a compilation options preset for balanced speed and debuggability.
    /// </summary>
    public static CompilationOptions Balanced() => new()
    {
        DirectBinaryEmit = true,
        SkipAstConstruction = false,
        EnableAggressiveOptimizations = true,
        EnableTypeCache = true
    };

    /// <summary>
    /// Creates a compilation options preset that preserves the traditional pipeline with human-readable output.
    /// </summary>
    public static CompilationOptions Traditional() => new()
    {
        DirectBinaryEmit = false,
        SkipAstConstruction = false,
        EnableAggressiveOptimizations = false,
        EnableTypeCache = true
    };
}
