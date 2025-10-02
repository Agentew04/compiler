namespace FortallCompiler.IL;

public class ILFunction
{
    public string Name;
    public List<ILParameter> Parameters;
    public List<ILInstruction> Instructions;
    public Ast.Type ReturnType;
    public string Label = "";
    
    public ILFunction(string name, List<ILParameter> parameters, Ast.Type returnType)
    {
        Name = name;
        Parameters = parameters;
        Instructions = [];
        ReturnType = returnType;
    }
}