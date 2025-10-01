namespace FortallCompiler.IL;

public class ILParameter {
    public string Name { get; } = string.Empty;
    public Ast.Type Type { get; } = Ast.Type.Void;
    
    public ILParameter(string name, Ast.Type type) {
        Name = name;
        Type = type;
    }
}