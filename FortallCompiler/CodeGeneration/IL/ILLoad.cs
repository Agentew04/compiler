namespace FortallCompiler.CodeGeneration.IL;

public class ILLoad : ILInstruction
{
    public string Dest;
    public object Value;

    public ILLoad(string dest, object value)
    {
        Dest = dest;
        Value = value;
    }

    public override string ToString() => $"load {Dest} = {Value}";
}