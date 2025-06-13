using Antlr4.Runtime;
using FortallCompiler.Antlr;
using FortallCompiler.Ast;

namespace FortallCompiler.Steps;

public class SyntaticAnalyzer {

    public (ProgramNode? program, bool success, List<Diagnostic> diagnostics) Analyze(Stream inputStream) {
        List<Diagnostic> diagnostics = [];
        AntlrInputStream input = new(inputStream);
        FortallLexer lexer = new(input);
        CommonTokenStream tokens = new(lexer);
        FortallParser parser = new(tokens);
        
        Action<int,int,string> errorHandler = (line, charPositionInLine, msg) => {
            diagnostics.Add(new Diagnostic(msg, line, charPositionInLine));
        };
        
        SyntaxErrorListener errorListener = new();
        parser.AddErrorListener(errorListener);
        errorListener.OnError += errorHandler;
        
        FortallParser.ProgramContext? program = parser.program();

        var visitor = new FortallVisitor();
        ProgramNode? ast = null;
        if (parser.NumberOfSyntaxErrors == 0 && program != null) {
            ast = (ProgramNode)visitor.VisitProgram(program);
        }
        errorListener.OnError -= errorHandler;

        return (ast, parser.NumberOfSyntaxErrors == 0, diagnostics);
    }
}