namespace FortallCompiler.Ast;

public abstract class AstNode
{
    public required int LineNumber { get; set; }
    public required int ColumnNumber { get; set; }
}