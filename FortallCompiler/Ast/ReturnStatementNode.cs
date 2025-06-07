namespace FortallCompiler.Ast;

public class ReturnStatementNode : StatementNode {
    public ExpressionNode? Expression { get; set; }
}