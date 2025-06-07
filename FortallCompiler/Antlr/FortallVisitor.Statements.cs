using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Antlr;

public partial class FortallVisitor {
    public override AstNode VisitDeclaration(FortallParser.DeclarationContext ctx) {
        Type type = VisitType(ctx.TYPE());
        string variableName = ctx.ID().GetText();
        ExpressionNode? expressionNode = null;
        if (ctx.expression() != null)
        {
            expressionNode = (ExpressionNode)VisitExpression(ctx.expression());
        }
        return new VariableDeclarationNode()
        {
            VariableName = variableName,
            VariableType = type,
            InitValue = expressionNode,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitAssignment(FortallParser.AssignmentContext ctx) {
        string variableName = ctx.ID().GetText();
        ExpressionNode expressionNode = (ExpressionNode)VisitExpression(ctx.expression());
        return new AssignmentNode()
        {
            VariableName = variableName,
            AssignedValue = expressionNode,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
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
        return new IfStatementNode()
        {
            Condition = condition,
            ThenBlock = thenBlock,
            ElseBlock = elseBlock,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitWhileStatement(FortallParser.WhileStatementContext ctx) {
        ExpressionNode condition = (ExpressionNode)VisitExpression(ctx.expression());
        BlockNode block = (BlockNode)VisitBlock(ctx.block());
        return new WhileStatementNode()
        {
            Condition = condition,
            Body = block,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitReturnStatement(FortallParser.ReturnStatementContext ctx) {
        ExpressionNode? expressionNode = null;
        if (ctx.expression() != null)
        {
            expressionNode = (ExpressionNode)VisitExpression(ctx.expression());
        }
        return new ReturnStatementNode()
        {
            Expression = expressionNode,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitIoStatement(FortallParser.IoStatementContext ctx) {
        if (ctx.expression() != null) {
            return new WriteNode() {
                Expression = (ExpressionNode)VisitExpression(ctx.expression()),
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        if (ctx.ID() != null) {
            return new ReadNode() {
                VariableName = ctx.ID().GetText(),
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        Console.WriteLine("Erro: io statement nao reconhecido");
        return null!;
    }
}