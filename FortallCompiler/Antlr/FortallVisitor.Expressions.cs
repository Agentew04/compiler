﻿using System.Text;
using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Antlr;

public partial class FortallVisitor {
    public override AstNode VisitExpression(FortallParser.ExpressionContext ctx) {
        return Visit(ctx.orExpr());
    }
    
    public override ExpressionNode VisitOrExpr(FortallParser.OrExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.andExpr(0));

        for (int i = 1; i < ctx.andExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // pega '||'
            ExpressionNode right = (ExpressionNode)Visit(ctx.andExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Boolean
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitAndExpr(FortallParser.AndExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.equalityExpr(0));

        for (int i = 1; i < ctx.equalityExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // pega '&&'
            ExpressionNode right = (ExpressionNode)Visit(ctx.equalityExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Boolean
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitEqualityExpr(FortallParser.EqualityExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.relationalExpr(0));

        for (int i = 1; i < ctx.relationalExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // '==' ou '!='
            ExpressionNode right = (ExpressionNode)Visit(ctx.relationalExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Boolean
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitRelationalExpr(FortallParser.RelationalExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.addExpr(0));

        for (int i = 1; i < ctx.addExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // '<', '>', '<=', '>='
            ExpressionNode right = (ExpressionNode)Visit(ctx.addExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Boolean
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitAddExpr(FortallParser.AddExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.mulExpr(0));

        for (int i = 1; i < ctx.mulExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // '+' ou '-'
            ExpressionNode right = (ExpressionNode)Visit(ctx.mulExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Integer
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitMulExpr(FortallParser.MulExprContext ctx)
    {
        ExpressionNode left = (ExpressionNode)Visit(ctx.unaryExpr(0));

        for (int i = 1; i < ctx.unaryExpr().Length; i++)
        {
            string op = ctx.GetChild(2 * i - 1).GetText(); // '*' ou '/'
            ExpressionNode right = (ExpressionNode)Visit(ctx.unaryExpr(i));

            left = new BinaryExpressionNode()
            {
                Operation = ParseBinaryOperationType(op),
                Left = left,
                Right = right,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Integer
            };
        }

        return left;
    }
    
    public override ExpressionNode VisitUnaryExpr(FortallParser.UnaryExprContext ctx)
    {
        if (ctx.ChildCount == 2 && ctx.GetChild(0).GetText() == "!")
        {
            return new UnaryExpressionNode()
            {
                Operation = UnaryOperationType.Not,
                Operand = (ExpressionNode)Visit(ctx.unaryExpr()),
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Boolean
            };
        }

        return (ExpressionNode)Visit(ctx.primary());
    }
    
    public override ExpressionNode VisitPrimary(FortallParser.PrimaryContext ctx)
    {
        if (ctx.ID() != null)
        {
            return new IdentifierExpressionNode() {
                Name = ctx.ID().GetText(),
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = Type.Void
            };
        }

        if (ctx.constant() != null)
        {
            var text = ctx.constant().GetText();

            object value;
            Type type;
            if (ctx.constant().NUMBER() != null) {
                value = int.Parse(text);
                type = Type.Integer;
            }else if (ctx.constant().STRING() != null)
            {
                value = text.Substring(1, text.Length - 2);
                type = Type.String;
            }else if (ctx.constant().BOOL() != null) {
                value = text == "true";
                type = Type.Boolean;
            }else {
                value = null!;
                type = Type.Void;
            }

            return new LiteralExpressionNode() {
                Value = value,
                LineNumber = ctx.Start.Line,
                ColumnNumber = ctx.Start.Column,
                ExpressionType = type,
                Type = type
            };
        }

        if (ctx.functionCall() != null) {
            return (FunctionCallExpressionNode)VisitFunctionCall(ctx.functionCall());
        }

        // Caso: (expression)
        return (ExpressionNode)Visit(ctx.expression());
    }


    private static BinaryOperationType ParseBinaryOperationType(string op)
    {
        return op switch
        {
            "+" => BinaryOperationType.Addition,
            "-" => BinaryOperationType.Subtraction,
            "*" => BinaryOperationType.Multiplication,
            "/" => BinaryOperationType.Division,
            "==" => BinaryOperationType.Equals,
            "!=" => BinaryOperationType.NotEquals,
            "<" => BinaryOperationType.LessThan,
            "<=" => BinaryOperationType.LessEqualThan,
            ">" => BinaryOperationType.GreaterThan,
            ">=" => BinaryOperationType.GreaterEqualThan,
            "&&" => BinaryOperationType.And,
            "||" => BinaryOperationType.Or,
            _ => throw new ArgumentException($"Operador binario desconhecido: {op}")
        };
    }
}