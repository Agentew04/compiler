using System.Text;

namespace FortallCompiler.Il;

public readonly struct Assembly {
    public string Name { get; init; }
    
    public bool IsExtern { get; init; }

    public override string ToString() {
        StringBuilder sb = new();
        sb.Append(".assembly ");
        if(IsExtern) sb.Append("extern ");
        sb.Append(Name);
        sb.AppendLine(" {}");
        return sb.ToString();
    }
}