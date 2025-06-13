using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.IL;

public class ILWrite : ILInstruction
{
    public ILAddress Src;
    public Type WriteType;
    
    public ILWrite(ILAddress src, Type writeType)
    {
        Src = src;
        WriteType = writeType;
    }

    public override string ToString() => $"write {WriteType} {Src}";
}