using System.Text;

namespace FortallCompiler.Fil;

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

    public void ToString(StringBuilder sb) {
        sb.Append($"func {Name}(");
        if (Parameters.Count > 0) {
            sb.Append(string.Join(", ", Parameters.Select(x => $"{x.Name}: {x.Type}")));
        }
        else {
            sb.Append("Void");
        }
        sb.AppendLine($") -> {ReturnType} {{");
        foreach (ILInstruction instruction in Instructions) {
            sb.AppendLine($"    {instruction}");
        }
        sb.AppendLine("}");
    }

    public override string ToString() {
        StringBuilder sb = new();
        ToString(sb);
        return sb.ToString();
    }
}