namespace FortallCompiler.CodeGeneration.IL;

public class ILRead : ILInstruction
{
    public ILAddress Dest;

    public ILRead(ILAddress dest)
    {
        Dest = dest;
    }
    
    public override string ToString() => $"read {Dest}";
}