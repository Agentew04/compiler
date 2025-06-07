namespace FortallCompiler.Ast;

public class FunctionCallExpressionNode : ExpressionNode {
    public string FunctionName { get; set; } = "";
    public List<ExpressionNode> Arguments { get; set; } = [];
}