using System;
using System.Collections.Generic;
using System.Text;

namespace WinterRose;

public class TempFile : FileStream
{
    private const string TEMP_DIR_NAME = "WinterRoseTempFiles";

    // Expose the full path for callers (safer than relying on FileStream.Name)
    public string FilePath { get; }

    /// <summary>
    /// Construct from an explicit path. Overwrites existing file by default.
    /// </summary>
    public TempFile(string path, FileShare share = FileShare.Read, FileAccess access = FileAccess.ReadWrite)
        : base(path, FileMode.Create, access, share)
    {
        FilePath = Path.GetFullPath(path);
    }

    /// <summary>
    /// Create a new temp file inside the WinterRose temp folder with an optional extension.
    /// Uses a Guid-based filename to avoid collisions.
    /// </summary>
    public TempFile(FileShare share = FileShare.Read, FileAccess access = FileAccess.ReadWrite, string extension = ".tmp")
        : this(CreateUniqueTempPath(extension), share, access)
    {
    }

    /// <summary>
    /// Copy the provided stream into a new temp file. The source stream will be read from its current position;
    /// if the source is seekable the constructor will rewind it to 0 before copying.
    /// The resulting TempFile is left open and positioned at 0 for reading.
    /// </summary>
    public TempFile(Stream other, FileShare share = FileShare.Read, FileAccess access = FileAccess.ReadWrite, string extension = ".tmp")
        : this(share, access, extension)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        // If source is seekable, ensure full data is copied from start
        if (other.CanSeek)
        {
            try { other.Position = 0; } catch { /* ignore if seeking fails */ }
        }

        other.CopyTo(this);
        Flush(true); // ensure OS buffers are pushed
        Position = 0;
    }

    /// <summary>
    /// Helper that ensures the WinterRose temp folder exists and returns a unique file path.
    /// </summary>
    private static string CreateUniqueTempPath(string extension)
    {
        if (string.IsNullOrEmpty(extension)) extension = ".tmp";
        if (!extension.StartsWith(".")) extension = "." + extension;

        string tempRoot = Path.Combine(Path.GetTempPath(), TEMP_DIR_NAME);
        Directory.CreateDirectory(tempRoot);

        // Try a few times to avoid improbable collisions
        for (int attempt = 0; attempt < 10; ++attempt)
        {
            string candidate = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + extension);
            // Using CreateNew would avoid a race, but we need the base constructor to create the file
            // so just ensure the file doesn't exist before returning the candidate path.
            if (!File.Exists(candidate)) return candidate;
        }

        // Last-resort: use a fallback name with ticks
        return Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + "_" + DateTime.UtcNow.Ticks + extension);
    }

    /// <summary>
    /// Expose a friendly Dispose that also attempts a best-effort cleanup.
    /// FileOptions.DeleteOnClose should cover most cases; attempt to delete if still present.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            // Best-effort: if file still exists (platforms where DeleteOnClose isn't applied),
            // attempt to remove it. Ignore failures.
            try
            {
                if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // swallow - temp files are ephemeral anyway
            }
        }
    }
}
