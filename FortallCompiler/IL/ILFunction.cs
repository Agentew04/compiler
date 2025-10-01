namespace FortallCompiler.IL;

public class ILFunction
{
    public string Name;
    public List<ILParameter> Parameters;
    public List<ILInstruction> Instructions;
    public string Label = "";
    
    public ILFunction(string name, List<ILParameter> parameters)
    {
        Name = name;
        Parameters = parameters;
        Instructions = [];
    }
}