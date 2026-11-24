using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>Streams held in memory: output is captured and <c>input()</c> reads end of stream at once.</summary>
internal sealed class FakeStreams : IStandardStreams {
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    /// <inheritdoc/>
    public TextWriter Out => _out;

    /// <inheritdoc/>
    public TextWriter Error => _error;

    /// <inheritdoc/>
    public TextReader In { get; } = new StringReader(string.Empty);

    /// <summary>Everything written to <see cref="Out"/>.</summary>
    public string OutText => _out.ToString();

    /// <summary>Everything written to <see cref="Error"/>.</summary>
    public string ErrorText => _error.ToString();
}
