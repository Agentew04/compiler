using System.Text;

namespace FortallCompiler.Il;

public class Class {

    public Accessibility Access { get; set; }
    
    public string Namespace { get; set; } = "DefaultNamespace";
    
    public string Name { get; set; } = "DefaultClassName";
    
    public ClassReference BaseClass { get; set; }

    public List<Field> Fields { get; set; } = [];

    public override string ToString() {
        StringBuilder sb = new();
        
        sb.Append(".class ");
        sb.Append(Access.ToIlString());
        sb.Append(" auto ansi beforefieldinit ");
        
        sb.Append(Namespace);
        sb.Append('.');
        sb.Append(Name);
        
        sb.Append(" extends ");
        sb.Append(BaseClass.ToString());
        sb.Append(" {");
        sb.AppendLine();
        
        // fields
        foreach (var field in Fields) {
            sb.Append("  ");
            sb.Append(field.ToString());
        }
        
        // functions
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}