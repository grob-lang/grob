namespace Grob.Vm;

/// <summary>
/// Three-state tag for a top-level global binding slot (§19.1, D-294). The VM
/// keeps one tag per top-level binding during startup so that a read of a
/// binding before its declaration has executed can be detected and reported as
/// <c>E5902</c> (circular initialisation).
/// </summary>
/// <remarks>
/// A slot moves <see cref="Uninitialised"/> → <see cref="Initialising"/> →
/// <see cref="Initialised"/> as its <c>DefineGlobal</c> runs. A
/// <c>GetGlobal</c> during startup that observes a slot in any state other than
/// <see cref="Initialised"/> is the circular-initialisation case. After the
/// top-level code completes the VM sets its <c>_startupComplete</c> flag and the
/// tag is no longer consulted.
/// </remarks>
internal enum SlotState : byte {
    /// <summary>The slot's <c>DefineGlobal</c> has not yet begun.</summary>
    Uninitialised = 0,

    /// <summary>The slot's right-hand side is mid-evaluation (transient within a single <c>DefineGlobal</c>).</summary>
    Initialising = 1,

    /// <summary>The slot's value has been stored; reads are valid.</summary>
    Initialised = 2,
}
