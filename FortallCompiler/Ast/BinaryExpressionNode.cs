namespace FortallCompiler.Ast;

public class BinaryExpressionNode : ExpressionNode {
    public BinaryOperationType Operation { get; set; }
    public required ExpressionNode Left { get; set; }
    public required ExpressionNode Right { get; set; }
}