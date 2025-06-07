namespace FortallCompiler.Ast;

public class LiteralExpressionNode : ExpressionNode {
    public Type Type { get; set; }
    public required object Value { get; set; }
}