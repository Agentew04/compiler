namespace FortallCompiler.CodeGeneration.IL;

public class ILFunction
{
    public string Name;
    public List<string> Paramters;
    public List<ILInstruction> Instructions;
    
    public ILFunction(string name, List<string> parameters)
    {
        Name = name;
        Paramters = parameters;
        Instructions = [];
    }
}