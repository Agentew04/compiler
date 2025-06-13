using FortallCompiler.Steps;

namespace FortallCompiler.Ast;

public class ProgramNode : AstNode
{
    public List<TopLevelNode> TopLevelNodes { get; } = [];
    
    public SemanticAnalyzer.ScopeData? ScopeData { get; set; }
    
    public Dictionary<string,string> StringLiterals { get; set; } = [];
}