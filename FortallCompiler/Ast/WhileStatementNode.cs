namespace FortallCompiler.Ast;

public class WhileStatementNode : StatementNode {
    public required ExpressionNode Condition { get; set; }
    public required BlockNode Body { get; set; }
}