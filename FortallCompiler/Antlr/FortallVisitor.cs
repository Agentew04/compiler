using Antlr4.Runtime.Tree;
using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Antlr;

public partial class FortallVisitor : FortallBaseVisitor<AstNode>
{
    public override AstNode VisitProgram(FortallParser.ProgramContext ctx) {
        ProgramNode program = new() {
            LineNumber = 0,
            ColumnNumber = 0
        };
        foreach (var node in ctx.toplevel())
        {
            program.TopLevelNodes.Add((TopLevelNode)VisitToplevel(node));
        }

        if (program.TopLevelNodes.Count == 0)
        {
            return program;
        }

        if (!program.TopLevelNodes.Where(x => x is FunctionNode).Cast<FunctionNode>()
                .Any(x => x.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase)))
        {
            return program;
        }

        return program;
    }

    public override AstNode VisitToplevel(FortallParser.ToplevelContext ctx)
    {
        if (ctx.field() != null)
        {
            return VisitField(ctx.field());
        }

        if (ctx.function() != null)
        {
            return VisitFunction(ctx.function());
        }
        Console.WriteLine("Erro: toplevel node nao reconhecido");
        return null!;
    }

    public override AstNode VisitField(FortallParser.FieldContext ctx)
    {
        Type type = VisitType(ctx.TYPE());
        string name = ctx.ID().GetText();
        ConstantNode? constant = null;
        if (ctx.constant() != null)
        {
             constant = (ConstantNode)VisitConstant(ctx.constant());
        }

        return new FieldDeclarationNode()
        {
            FieldName = name,
            FieldType = type,
            InitValue = constant,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }
    
    public override AstNode VisitFunction(FortallParser.FunctionContext ctx)
    {
        string functionName = ctx.ID().GetText();
        FortallParser.ParamListContext? parameters = ctx.paramList();
        List<ParameterNode> parameterNodes = [];
        if (parameters != null)
        {
            parameterNodes.AddRange(parameters.param().Select(paramCtx => (ParameterNode)VisitParam(paramCtx)));
        }

        Type returnType = VisitType(ctx.TYPE());

        BlockNode block = (BlockNode)VisitBlock(ctx.block());
        
        return new FunctionNode()
        {
            Name = functionName,
            Parameters = parameterNodes,
            ReturnType = returnType,
            Body = block,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitParam(FortallParser.ParamContext ctx)
    {
        Type type = VisitType(ctx.TYPE());
        string name = ctx.ID().GetText();
        return new ParameterNode()
        {
            Name = name,
            ParameterType = type,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitConstant(FortallParser.ConstantContext ctx)
    {
        if (ctx.STRING() != null)
        {
            return new ConstantNode(Type.String, ctx.STRING().ToString() ?? "") {
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        if (ctx.NUMBER() != null)
        {
            return new ConstantNode(Type.Integer, int.Parse(ctx.NUMBER().ToString() ?? "0")){
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        if (ctx.BOOL() != null)
        {
            return new ConstantNode(Type.Boolean, ctx.BOOL().ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false){
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        Console.WriteLine("Erro: constante nao reconhecida");
        return null!;
    }

    public override AstNode VisitBlock(FortallParser.BlockContext ctx) {
        FortallParser.StatementContext[]? stmts = ctx.statement();

        List<StatementNode> statementNodes = [];
        if (stmts != null)
        {
            foreach (var stmt in stmts)
            {
                var statementNode = (StatementNode)VisitStatement(stmt);
                if (statementNode != null)
                {
                    statementNodes.Add(statementNode);
                }
            }
        }
        return new BlockNode()
        {
            Statements = statementNodes,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitStatement(FortallParser.StatementContext ctx) {
        if (ctx.declaration() != null) {
            return VisitDeclaration(ctx.declaration());
        }
        if (ctx.assignment() != null) {
            return VisitAssignment(ctx.assignment());
        }
        if (ctx.ifStatement() != null) {
            return VisitIfStatement(ctx.ifStatement());
        }
        if (ctx.whileStatement() != null) {
            return VisitWhileStatement(ctx.whileStatement());
        }
        if (ctx.returnStatement() != null) {
            return VisitReturnStatement(ctx.returnStatement());
        }
        if (ctx.ioStatement() != null) {
            return VisitIoStatement(ctx.ioStatement());
        }
        if(ctx.functionCall() != null) {
            var callExpression = (FunctionCallExpressionNode)VisitFunctionCall(ctx.functionCall());
            return new FunctionCallStatementNode()
            {
                FunctionCallExpression = callExpression,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column
            };
        }
        return new EmptyStatement() {
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column
        };
    }

    public override AstNode VisitFunctionCall(FortallParser.FunctionCallContext ctx) {
        string functionName = ctx.ID().GetText();
        List<ExpressionNode> arguments = [];
        var exprs = ctx.expression();
        if (exprs != null)
        {
            arguments.AddRange(exprs.Select(expr => (ExpressionNode)VisitExpression(expr)));
        }
        return new FunctionCallExpressionNode()
        {
            FunctionName = functionName,
            Arguments = arguments,
            LineNumber = ctx.Start.Line,
            ColumnNumber = ctx.Start.Column,
            ExpressionType = Type.Void
        };
    }


    private static Type VisitType(ITerminalNode? type)
    {
        if (type is null)
        {
            return Type.Void;
        }
        return type.ToString() switch
        {
            "str" => Type.String,
            "int" => Type.Integer,
            "bool" => Type.Boolean,
            _ => Type.Void
        };
    }
    
    
}