namespace FortallCompiler.AstNodes;

public class ParameterNode : AstNode
{
    public Type? ParameterType { get; set; }
    public string Name { get; set; } = string.Empty;
}