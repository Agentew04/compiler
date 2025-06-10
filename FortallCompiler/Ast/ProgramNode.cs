namespace FortallCompiler.Ast;

public class ProgramNode : AstNode
{
    public List<TopLevelNode> TopLevelNodes { get; } = [];
    
    public SemanticAnalyzer.ScopeData? ScopeData { get; set; }
}