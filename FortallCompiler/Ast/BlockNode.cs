using FortallCompiler.Steps;

namespace FortallCompiler.Ast;

public class BlockNode : AstNode
{
    public List<StatementNode> Statements = [];
    
    public SemanticAnalyzer.ScopeData? ScopeData { get; set; }
}