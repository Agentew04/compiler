using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.CodeGeneration.IL;

public class ILWrite : ILInstruction
{
    public string Src;
    public Type WriteType;
    
    public ILWrite(string src, Type writeType)
    {
        Src = src;
        WriteType = writeType;
    }

    public override string ToString() => $"write {WriteType} {Src}";
}