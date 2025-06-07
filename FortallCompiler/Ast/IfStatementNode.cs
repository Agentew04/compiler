namespace FortallCompiler.Ast;

public class IfStatementNode : StatementNode {
    public required ExpressionNode Condition { get; set; }
    public required BlockNode ThenBlock { get; set; }
    public BlockNode? ElseBlock { get; set; }
}