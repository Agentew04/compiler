namespace FortallCompiler.AstNodes;

public class FunctionNode : TopLevelNode
{
    public string Name { get; set; } = "";
    public List<ParameterNode> Parameters { get; set; } = [];
    public Type ReturnType { get; set; }
    public List<StatementNode> Body { get; set; } = [];
}