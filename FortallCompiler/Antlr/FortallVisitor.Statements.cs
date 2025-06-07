using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Antlr;

public partial class FortallVisitor {
    public override AstNode VisitDeclaration(FortallParser.DeclarationContext context) {
        Type type = VisitType(context.TYPE());
        string variableName = context.ID().GetText();
        ExpressionNode? expressionNode = null;
        if (context.expression() != null)
        {
            expressionNode = (ExpressionNode)VisitExpression(context.expression());
        }
        Console.WriteLine($"Declaracao de variavel {variableName} de tipo {type} {(expressionNode == null ? "sem valor inicial" : $"com valor inicial {context.expression().GetText()}")}");
        return new VariableDeclarationNode()
        {
            VariableName = variableName,
            VariableType = type,
            InitValue = expressionNode
        };
    }

    public override AstNode VisitAssignment(FortallParser.AssignmentContext ctx) {
        string variableName = ctx.ID().GetText();
        ExpressionNode expressionNode = (ExpressionNode)VisitExpression(ctx.expression());
        Console.WriteLine($"Atribuicao de {variableName} com valor {ctx.expression().GetText()}");
        return new AssignmentNode()
        {
            VariableName = variableName,
            AssignedValue = expressionNode
        };
    }

    public override AstNode VisitIfStatement(FortallParser.IfStatementContext ctx) {
        ExpressionNode condition = (ExpressionNode)VisitExpression(ctx.expression());
        BlockNode thenBlock = (BlockNode)VisitBlock(ctx.block(0));
        BlockNode? elseBlock = null;
        if (ctx.block().Length > 1)
        {
            elseBlock = (BlockNode)VisitBlock(ctx.block(1));
        }
        Console.WriteLine($"If statement with condition {ctx.expression().GetText()}");
        return new IfStatementNode()
        {
            Condition = condition,
            ThenBlock = thenBlock,
            ElseBlock = elseBlock
        };
    }

    public override AstNode VisitWhileStatement(FortallParser.WhileStatementContext ctx) {
        ExpressionNode condition = (ExpressionNode)VisitExpression(ctx.expression());
        BlockNode block = (BlockNode)VisitBlock(ctx.block());
        Console.WriteLine($"While statement with condition {ctx.expression().GetText()}");
        return new WhileStatementNode()
        {
            Condition = condition,
            Body = block
        };
    }

    public override AstNode VisitReturnStatement(FortallParser.ReturnStatementContext ctx) {
        ExpressionNode? expressionNode = null;
        if (ctx.expression() != null)
        {
            expressionNode = (ExpressionNode)VisitExpression(ctx.expression());
        }
        Console.WriteLine($"Return statement with value {ctx.expression()?.GetText() ?? "none"}");
        return new ReturnStatementNode()
        {
            Expression = expressionNode
        };
    }

    public override AstNode VisitIoStatement(FortallParser.IoStatementContext ctx) {
        if (ctx.expression() != null) {
            return new WriteNode() {
                Expression = (ExpressionNode)VisitExpression(ctx.expression()),
            };
        }
        if (ctx.ID() != null) {
            return new ReadNode() {
                VariableName = ctx.ID().GetText(),
            };
        }
        Console.WriteLine("Erro: io statement nao reconhecido");
        return null!;
    }
}