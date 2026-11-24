using System.Text;

namespace Grob.Torture.Campaign;

/// <summary>
/// A worker's journal: one line, <c>&lt;program&gt;\t&lt;state&gt;</c>, naming the program the worker is
/// on and the pipeline phase it is in, or <see cref="Minimising"/>. A worker that dies cannot say
/// what killed it, as a stack overflow (R-70) ends the process with no handler run, so its
/// supervisor reads the journal instead (gate report §4.4). Empty between programs.
/// </summary>
/// <remarks>
/// The file stays open and each write replaces its contents and is flushed to the operating
/// system, not to the disk: a process that dies leaves its written bytes behind, and only a
/// machine that stops would lose them. That keeps a write to a few microseconds, which matters at
/// six writes a program over a campaign of hundreds of thousands. Opening a journal empties it.
/// </remarks>
internal sealed class Journal : IDisposable {
    /// <summary>The state a worker writes while it minimises a program's finding.</summary>
    public const string Minimising = "minimise";

    private readonly FileStream _stream;

    /// <summary>Opens, and empties, the journal at <paramref name="path"/>.</summary>
    public Journal(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    /// <summary>Records that the worker is on <paramref name="programId"/>, in <paramref name="state"/>.</summary>
    public void Write(string programId, string state) => Replace(Encoding.UTF8.GetBytes($"{programId}\t{state}\n"));

    /// <summary>Records that the worker is between programs.</summary>
    public void Clear() => Replace([]);

    /// <inheritdoc/>
    public void Dispose() => _stream.Dispose();

    /// <summary>The program and state a journal holds, or <see langword="null"/> when it is empty or missing.</summary>
    public static (string ProgramId, string State)? Read(string path) {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string[] parts = reader.ReadToEnd().TrimEnd('\n').Split('\t');
        return parts.Length == 2 ? (parts[0], parts[1]) : null;
    }

    private void Replace(byte[] bytes) {
        _stream.Position = 0;
        _stream.Write(bytes);
        _stream.SetLength(bytes.Length);
        _stream.Flush(flushToDisk: false);
    }
}
