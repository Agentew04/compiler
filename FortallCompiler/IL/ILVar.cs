using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.IL;

public class ILVar : ILInstruction
{
    public string Name;
    public Type Type;

    public ILVar(string name, Type type)
    {
        Name = name;
        Type = type;
    }
    
    public override string ToString() => $"var {Name}: {Type}";
}