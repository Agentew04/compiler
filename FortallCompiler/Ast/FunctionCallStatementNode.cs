namespace FortallCompiler.Ast;

public class FunctionCallStatementNode : StatementNode {
    public required FunctionCallExpressionNode FunctionCallExpression { get; set; }
}