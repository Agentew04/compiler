namespace FortallCompiler.IL;

public class ILLoadPtr : ILInstruction
{
    public ILAddress Dest;
    public ILAddress Src;

    public ILLoadPtr(ILAddress dest, ILAddress src)
    {
        Dest = dest;
        Src = src;
    }
    
    public override string ToString() => $"loadptr {Dest} {Src}";
}