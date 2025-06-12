using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.CodeGeneration.IL;

public class ILRead : ILInstruction
{
    public ILAddress Dest;
    public Type ReadType;

    public ILRead(ILAddress dest, Type readType)
    {
        Dest = dest;
        ReadType = readType;
    }
    
    public override string ToString() => $"read {Dest}";
}