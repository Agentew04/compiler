namespace FortallCompiler.CodeGeneration.IL;

public class ILRead : ILInstruction
{
    public string Dest;

    public ILRead(string dest)
    {
        Dest = dest;
    }
    
    public override string ToString() => $"read {Dest}";
}