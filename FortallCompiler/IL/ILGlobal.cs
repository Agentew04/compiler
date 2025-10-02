using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.IL;

public class ILGlobalVariable
{
    public string Name = "";
    public Type Type;
    public object? Value;

    public override string ToString() => $"global {Name}: {Type}" + (Value != null ? $" = {Value}" : "");
}