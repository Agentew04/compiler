namespace FortallCompiler.AstNodes;

public class ConstantNode : AstNode
{
    public ConstantNode(Type type, object value)
    {
        Type = type;
        Value = value;
    }
    
    public Type Type { get; }
    public object Value { get; }
}