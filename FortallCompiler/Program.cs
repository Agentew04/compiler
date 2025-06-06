using System.Reflection;
using Antlr4.Runtime;
using FortallCompiler.Antlr;
using FortallCompiler.AstNodes;

namespace FortallCompiler;

class Program
{
    static void Main(string[] args)
    {
        Stream? s = Assembly.GetExecutingAssembly()?.GetManifestResourceStream("FortallCompiler.test.all");
        if (s is null)
        {
            Console.WriteLine("sem arquivo test.all");
            return;
        }

        AntlrInputStream input = new(s);
        FortallLexer lexer = new(input);
        CommonTokenStream tokens = new(lexer);
        tokens.Fill();
        
        foreach (var token in tokens.GetTokens())
        {
            Console.WriteLine($"<{lexer.Vocabulary.GetSymbolicName(token.Type)}> '{token.Text}'");
        }
        FortallParser parser = new(tokens);
        
        parser.AddErrorListener(new SyntaxErrorListener());
        
        FortallParser.ProgramContext? program = parser.program();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            Console.WriteLine("Erro de sintaxe");
            return;
        }
        var visitor = new FortallVisitor();
        AstNode? ast = visitor.VisitProgram(program);
    }
}