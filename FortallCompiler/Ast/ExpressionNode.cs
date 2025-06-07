namespace FortallCompiler.Ast;

public abstract class ExpressionNode : AstNode
{
    public required Type ExpressionType { get; set; }
}