using System.Security.Cryptography;
using System.Text;

using Grob.Core;
using Grob.Core.NamedTypes;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>guid</c> module (D-149, D-303, D-336 — Sprint 8 Increment D): generation
/// (<c>newV4</c>/<c>newV7</c> on <see cref="IRandomSource"/>/<see cref="IClock"/>, the
/// variadic <c>newV5</c>), parsing (<c>parse</c> throwing <c>ParseError</c> through the
/// native-throw seam, <c>tryParse</c> returning nil), the well-known RFC 4122 namespaces
/// and <c>empty</c>, and the registered <c>toString()</c> that makes <c>ValueDisplay</c>
/// (D-336) render the canonical form. A <c>guid</c> runtime value is a
/// <see cref="GrobStruct"/> named <c>"guid"</c> with exactly one hidden field
/// (<see cref="ValueFieldName"/>) holding the canonical lowercase-hyphenated string —
/// this is the only place outside <c>Grob.Vm</c>'s <c>GuidNatives</c> that convention is
/// spelled out (the two cannot share code: <c>Grob.Stdlib</c> and <c>Grob.Vm</c> are DAG
/// siblings, neither referencing the other), so it must stay in lockstep with
/// <c>GuidNatives.ValueFieldName</c>/<c>TypeName</c>. Registers exactly the qualified
/// names listed in the compile-time twin, <c>NamespaceRegistry</c>'s <c>guid</c> entry in
/// <c>Grob.Compiler</c>.
/// </summary>
public sealed class GuidPlugin : IGrobPlugin {
    /// <summary>The hidden field name storing a <c>guid</c> value's canonical string form.</summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>guid</c> value carries.</summary>
    internal const string TypeName = "guid";

    // RFC 4122 §4.3 well-known namespaces.
    private static readonly Guid DnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid UrlNamespace = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid OidNamespace = Guid.Parse("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

    private readonly IRandomSource _randomSource;
    private readonly IClock _clock;

    /// <summary>
    /// Initialises the plugin with the <see cref="IRandomSource"/>/<see cref="IClock"/>
    /// its generation natives draw from.
    /// </summary>
    public GuidPlugin(IRandomSource randomSource, IClock clock) {
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentNullException.ThrowIfNull(clock);
        _randomSource = randomSource;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "guid";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("guid.newV4", new NativeFunction("guid.newV4", 0, (_, _) =>
            FromGuid(CreateVersion4())));

        registrar.RegisterNative("guid.newV7", new NativeFunction("guid.newV7", 0, (_, _) =>
            FromGuid(CreateVersion7())));

        registrar.RegisterNative("guid.newV5", new NativeFunction("guid.newV5", 1, (args, _) => {
            Guid ns = ToGuid(args[0].AsStruct());
            string name = string.Concat(args.Skip(1).Select(a => a.AsString()));
            return FromGuid(CreateVersion5(ns, name));
        }));

        registrar.RegisterNative("guid.parse", new NativeFunction("guid.parse", 1, (args, _) => {
            string s = args[0].AsString();
            if (!Guid.TryParse(s, out Guid parsed)) {
                throw new NativeFaultException(
                    "ParseError", ErrorCatalog.E5701.Code, $"guid.parse: '{s}' is not a valid guid.");
            }
            return FromGuid(parsed);
        }));

        registrar.RegisterNative("guid.tryParse", new NativeFunction("guid.tryParse", 1, (args, _) =>
            Guid.TryParse(args[0].AsString(), out Guid parsed) ? FromGuid(parsed) : GrobValue.Nil));

        registrar.RegisterConstant("guid.empty", FromGuid(Guid.Empty));
        registrar.RegisterConstant("guid.namespaces.dns", FromGuid(DnsNamespace));
        registrar.RegisterConstant("guid.namespaces.url", FromGuid(UrlNamespace));
        registrar.RegisterConstant("guid.namespaces.oid", FromGuid(OidNamespace));

        // D-356: the renderer itself now lives on the NamedTypeRegistry entry (the
        // single source of truth also consulted by Grob.Compiler/Grob.Vm) — this call
        // only wires it into ValueDisplay's runtime registry, preserving the D-336
        // credential-ordering guarantee unchanged.
        registrar.RegisterToString(NamedTypeRegistry.Guid.CanonicalName, NamedTypeRegistry.Guid.ToStringRenderer);
    }

    // -----------------------------------------------------------------------
    // Runtime representation — a GrobStruct with one hidden field. Must stay in
    // lockstep with Grob.Vm.GuidNatives (DAG siblings; no shared code possible).
    // -----------------------------------------------------------------------

    private static GrobValue FromGuid(Guid g) => GrobValue.FromStruct(new GrobStruct(
        TypeName, [new KeyValuePair<string, GrobValue>(ValueFieldName, GrobValue.FromString(g.ToString("D")))]));

    private static string CanonicalString(GrobStruct receiver) => receiver.GetField(ValueFieldName).AsString();

    private static Guid ToGuid(GrobStruct receiver) => Guid.Parse(CanonicalString(receiver));

    // -----------------------------------------------------------------------
    // Generation — RFC 9562 (formerly RFC 4122) versions 4, 5 and 7. Versions 1 and 3
    // are excluded from v1 (D-149).
    // -----------------------------------------------------------------------

    /// <summary>Version 4 — random, drawn from <see cref="IRandomSource"/>.</summary>
    private Guid CreateVersion4() {
        byte[] bytes = RandomBytes(16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40); // version 4
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant RFC 4122
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// Version 7 — time-ordered: a 48-bit big-endian Unix-epoch-millisecond prefix from
    /// <see cref="IClock"/>, random tail bits from <see cref="IRandomSource"/>.
    /// </summary>
    private Guid CreateVersion7() {
        long unixMs = new DateTimeOffset(_clock.UtcNow).ToUnixTimeMilliseconds();
        byte[] bytes = new byte[16];
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        byte[] tail = RandomBytes(10);
        Array.Copy(tail, 0, bytes, 6, 10);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant RFC 4122
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// Version 5 — deterministic: SHA-1 of the namespace's bytes followed by the UTF-8
    /// bytes of <paramref name="name"/> (the caller's already-concatenated variadic name
    /// segments), version/variant bits set per RFC 4122 §4.3.
    /// </summary>
    private static Guid CreateVersion5(Guid namespaceId, string name) {
        Span<byte> namespaceBytes = stackalloc byte[16];
        namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);

        byte[] combined = new byte[16 + nameBytes.Length];
        namespaceBytes.CopyTo(combined);
        nameBytes.CopyTo(combined, 16);

        // SHA-1 is mandated by RFC 4122 §4.3 for version-5 UUID construction — not a
        // choice made for cryptographic strength. It is used here purely as a
        // deterministic name-to-bits function so the same (namespace, name) pair always
        // produces the same GUID (Bicep's guid() interop model, D-149); switching to a
        // stronger hash would produce a value that is no longer RFC-4122-compliant and
        // would not interoperate with any other implementation's UUIDv5. SonarCloud's
        // S4790 ("use a stronger hashing algorithm") is suppressed for this file in
        // .github/workflows/sonarcloud.yml with this same rationale.
        byte[] hash = SHA1.HashData(combined);
        byte[] bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant RFC 4122
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>Draws <paramref name="count"/> random bytes from <see cref="_randomSource"/>.</summary>
    private byte[] RandomBytes(int count) {
        var bytes = new byte[count];
        Span<byte> chunk = stackalloc byte[8];
        for (int i = 0; i < count; i += 8) {
            long v = _randomSource.NextInt(long.MinValue, long.MaxValue);
            // Write directly into a stack-allocated span rather than
            // BitConverter.GetBytes(v), which allocates a new array every iteration
            // (CodeRabbit review, PR #133).
            BitConverter.TryWriteBytes(chunk, v);
            int n = Math.Min(8, count - i);
            chunk[..n].CopyTo(bytes.AsSpan(i, n));
        }
        return bytes;
    }
}
