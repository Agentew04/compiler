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

        return new FunctionNode()
        {
            Name = functionName,
            Parameters = parameterNodes,
            ReturnType = returnType,
            // Body = (BlockNode)VisitBlock(ctx.block())
        };
    }

    public Type VisitType(ITerminalNode type)
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