namespace Grob.Compiler.Binding;

/// <summary>
/// A script function's parameter list as the binder sees it: a parameter has a default
/// when its declaration carries a default expression.
/// </summary>
internal readonly struct ScriptParameters(FnDecl fn) : IBindableParameters {
    /// <inheritdoc/>
    public int Count => fn.Parameters.Count;

    /// <inheritdoc/>
    public string NameAt(int index) => fn.Parameters[index].Name;

    /// <inheritdoc/>
    public bool HasDefaultAt(int index) => fn.Parameters[index].DefaultValue is not null;
}
