namespace FortallCompiler.IL;

public class ILCall : ILInstruction
{
    public ILAddress? Dest; // pode ser null p funcoes void
    public string FunctionName;
    public List<ILAddress> Arguments;

    public ILCall(ILAddress dest, string functionName, List<ILAddress> arguments)
    {
        Dest = dest;
        FunctionName = functionName;
        Arguments = arguments;
    }
    
    public override string ToString() => 
        $"Call {(Dest != null ? $"{Dest} = " : "")}{FunctionName}({string.Join(", ", Arguments)})";
}