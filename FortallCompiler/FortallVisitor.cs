using Antlr4.Runtime.Tree;
using FortallCompiler.Antlr;
using FortallCompiler.AstNodes;
using Type = FortallCompiler.AstNodes.Type;

namespace FortallCompiler;

public class FortallVisitor : FortallBaseVisitor<AstNode>
{
    public override AstNode VisitProgram(FortallParser.ProgramContext ctx)
    {
        ProgramNode program = new();
        foreach (var node in ctx.toplevel())
        {
            program.TopLevelNodes.Add((TopLevelNode)VisitToplevel(node));
        }

        if (program.TopLevelNodes.Count == 0)
        {
            Console.WriteLine("No top-level nodes found in the program.");
            return program;
        }

        if (!program.TopLevelNodes.Where(x => x is FunctionNode).Cast<FunctionNode>()
                .Any(x => x.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase)))
        {
            Console.WriteLine("Nao achei funcao main");
            return program;
        }

        Console.WriteLine("Program with {0} top-level nodes", program.TopLevelNodes.Count);
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
        return null;
    }

    public override AstNode VisitField(FortallParser.FieldContext ctx)
    {
        Type type = VisitType(ctx.TYPE());
        string name = ctx.ID().ToString() ?? "null";
        ConstantNode? constant = null;
        if (ctx.constant() != null)
        {
             constant = (ConstantNode)VisitConstant(ctx.constant());
        }

        Console.WriteLine($"Campo {name} de tipo {type} {(constant == null ? "sem valor" : $"com valor {constant.Value}")}");
        return new FieldDeclarationNode()
        {
            FieldName = name,
            FieldType = type,
            InitValue = constant
        };
    }
    
    public override AstNode VisitFunction(FortallParser.FunctionContext ctx)
    {
        string functionName = ctx.ID().ToString() ?? "null";
        FortallParser.ParamListContext? parameters = ctx.paramList();
        List<ParameterNode> parameterNodes = [];
        if (parameters != null)
        {
            parameterNodes.AddRange(parameters.param().Select(paramCtx => (ParameterNode)VisitParam(paramCtx)));
        }

        Type returnType = VisitType(ctx.TYPE());

        List<StatementNode> body = [];
        if (ctx.block() != null)
        {
            body.AddRange(ctx.block().statement().Select(stmtCtx => (StatementNode)VisitStatement(stmtCtx)));
        }

        Console.WriteLine("Funcao {0} com {1} parametros, tipo de retorno {2} e {3} statements", functionName, parameterNodes.Count, returnType, body.Count);
        return new FunctionNode()
        {
            Name = functionName,
            Parameters = parameterNodes,
            ReturnType = returnType,
            Body = body
        };
    }

    public override AstNode VisitParam(FortallParser.ParamContext ctx)
    {
        Type type = VisitType(ctx.TYPE());
        string name = ctx.ID().ToString() ?? "null";
        Console.WriteLine($"Parametro {name} de tipo {type}");
        return new ParameterNode()
        {
            Name = name,
            ParameterType = type
        };
    }

    public override AstNode VisitConstant(FortallParser.ConstantContext ctx)
    {
        if (ctx.STRING() != null)
        {
            return new ConstantNode(Type.String, ctx.STRING().ToString() ?? "");
        }
        if (ctx.NUMBER() != null)
        {
            return new ConstantNode(Type.Integer, int.Parse(ctx.NUMBER().ToString() ?? "0"));
        }
        if (ctx.BOOL() != null)
        {
            return new ConstantNode(Type.Boolean, ctx.BOOL().ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);
        }
        Console.WriteLine("Erro: constante nao reconhecida");
        return null;
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