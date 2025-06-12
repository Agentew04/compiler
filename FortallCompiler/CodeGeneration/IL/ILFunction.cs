namespace FortallCompiler.CodeGeneration.IL;

public class ILFunction
{
    public string Name;
    public List<string> Parameters;
    public List<ILInstruction> Instructions;
    public string Label;
    
    public ILFunction(string name, List<string> parameters)
    {
        Name = name;
        Parameters = parameters;
        Instructions = [];
    }
}