namespace FortallCompiler.Ast;

public class UnaryExpressionNode : ExpressionNode {
    public UnaryOperationType Operation { get; set; }
    public required ExpressionNode Operand { get; set; }
}