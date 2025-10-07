using System.Text;

namespace FortallCompiler.Il;

public readonly struct Field {
    
    public Accessibility Access { get; init; }
    public bool IsStatic { get; init; }
    public Type Type { get; init; }
    public string Name { get; init; }

    public override string ToString() {
        StringBuilder sb = new();
        sb.Append(".field ");
        sb.Append(Access.ToIlString());
        if(IsStatic) sb.Append("static ");
        sb.Append(Type.ToString());
        sb.Append(' ');
        sb.Append('\'');
        sb.Append(Name);
        sb.Append('\'');
        sb.AppendLine();
        
        return sb.ToString();
    }
}