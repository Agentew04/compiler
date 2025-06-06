namespace FortallCompiler.AstNodes;

public class ProgramNode : AstNode
{
    public List<TopLevelNode> TopLevelNodes { get; } = [];
}