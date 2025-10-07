namespace FortallCompiler.Fil;

public class ILMove : ILInstruction
{
    public ILAddress Dest, Src;

    public ILMove(ILAddress dest, ILAddress src)
    {
        Dest = dest;
        Src = src;
    }
    
    public override string ToString() => $"move {Dest} <= {Src}";
}