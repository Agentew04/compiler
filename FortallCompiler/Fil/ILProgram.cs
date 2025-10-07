using System.Text;

namespace FortallCompiler.Fil;

public class ILProgram
{
    public List<ILFunction> Functions { get; set; } = [];
    public List<ILGlobalVariable> Globals { get; set; } = [];

    public string MainLabel { get; set; } = null!;

    public override string ToString()
    {
        StringBuilder sb = new();
        foreach (ILGlobalVariable global in Globals)
        {
            sb.AppendLine(global.ToString());
        }
        sb.AppendLine();
        foreach (ILFunction function in Functions) {
            function.ToString(sb);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}