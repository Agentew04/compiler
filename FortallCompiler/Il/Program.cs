using System.Text;

namespace FortallCompiler.Il;

public class Program {
    public List<Assembly> ReferencedAssemblies { get; set; } = [];
    public Assembly Assembly { get; set; }
    public Module Module { get; set; }
    
    public List<Class> Classes { get; set; } = [];

    public override string ToString() {
        StringBuilder sb = new();
        
        // referenced assemblies
        foreach (Assembly ra in ReferencedAssemblies) {
            sb.AppendLine(ra.ToString());
        }
        sb.AppendLine();
        sb.AppendLine(Assembly.ToString());
        sb.AppendLine(Module.ToString());
        sb.AppendLine();
        foreach (Class c in Classes) {
            sb.AppendLine(c.ToString());
        }


        return sb.ToString();
    }
}