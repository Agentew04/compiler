namespace FortallCompiler.CodeGeneration.IL;

public class ILMove : ILInstruction
{
    public string Dest, Src;

    public ILMove(string dest, string src)
    {
        Dest = dest;
        Src = src;
    }
    
    public override string ToString() => $"{Dest} = {Src}";
}