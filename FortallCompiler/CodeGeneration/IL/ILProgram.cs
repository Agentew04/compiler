namespace FortallCompiler.CodeGeneration.IL;

public class ILProgram
{
    public List<ILFunction> Functions = [];
    public Dictionary<string, string> Globals = [];
}