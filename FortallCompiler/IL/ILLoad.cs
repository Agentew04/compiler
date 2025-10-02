namespace FortallCompiler.IL;

public class ILLoad : ILInstruction
{
    public ILAddress Dest;
    public object Value;

    public ILLoad(ILAddress dest, object value)
    {
        Dest = dest;
        Value = value;
    }

    public override string ToString() => $"load {Dest} <= {Value}";
}