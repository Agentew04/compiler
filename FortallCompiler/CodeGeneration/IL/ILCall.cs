namespace FortallCompiler.CodeGeneration.IL;

public class ILCall : ILInstruction
{
    public string? Dest; // pode ser null p funcoes void
    public string FunctionName;
    public List<string> Arguments;

    public ILCall(string dest, string functionName, List<string> arguments)
    {
        Dest = dest;
        FunctionName = functionName;
        Arguments = arguments;
    }
    
    public override string ToString() => 
        $"{(Dest != null ? $"{Dest} = " : "")}{FunctionName}({string.Join(", ", Arguments)})";
}