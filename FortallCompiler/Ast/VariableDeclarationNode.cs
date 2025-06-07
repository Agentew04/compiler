using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Ast;

public class VariableDeclarationNode : StatementNode {
    public Type VariableType { get; set; }
    public string VariableName { get; set; } = "";
    public ExpressionNode? InitValue { get; set; }
}