namespace FortallCompiler.Ast;

public class FieldDeclarationNode : TopLevelNode
{
    public Type FieldType { get; set; }
    public string FieldName { get; set; } = "";
    public ConstantNode? InitValue { get; set; }
}