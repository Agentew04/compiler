namespace FortallCompiler.Ast;

public class AssignmentNode : StatementNode {

    public string VariableName { get; set; } = "";
    public required ExpressionNode AssignedValue { get; set; }
}