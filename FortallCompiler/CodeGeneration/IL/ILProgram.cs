using System.Text;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.CodeGeneration.IL;

public class ILProgram
{
    public List<ILFunction> Functions { get; set; } = [];
    public List<ILGlobalVariable> Globals { get; set; } = [];
    
    public string MainLabel { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new();
        foreach (ILGlobalVariable global in Globals)
        {
            sb.AppendLine(global.ToString());
        }
        sb.AppendLine();
        foreach (ILFunction function in Functions)
        {
            sb.AppendLine($"# {function.Name}({string.Join(", ", function.Parameters)})");
            foreach (ILInstruction instruction in function.Instructions)
            {
                sb.AppendLine("  " + instruction.ToString());
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}