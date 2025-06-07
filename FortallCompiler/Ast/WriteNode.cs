namespace FortallCompiler.Ast;

public class WriteNode : IoNode {
    public required ExpressionNode Expression { get; set; }
}