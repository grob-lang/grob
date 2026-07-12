using Grob.Vm;
using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment A: <see cref="IoPlugin"/> is the print/exit formalisation
/// placeholder (D-343) — it registers no callable (print/exit stay on their existing
/// dedicated opcodes), but exists as a uniform plugin-registration entry, so it still
/// gets direct coverage rather than being exercised only incidentally through
/// <c>Grob.Integration.Tests</c>.
/// </summary>
public sealed class IoPluginTests {
    [Fact]
    public void Name_IsIo() {
        Assert.Equal("io", new IoPlugin().Name);
    }

    [Fact]
    public void Register_RegistersNoGlobals() {
        var vm = new VirtualMachine(new StringWriter());
        new IoPlugin().Register(vm);

        Assert.Empty(vm.Globals);
    }

    [Fact]
    public void Register_NullRegistrar_Throws() {
        Assert.Throws<ArgumentNullException>(() => new IoPlugin().Register(null!));
    }
}
