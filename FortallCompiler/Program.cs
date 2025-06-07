using System.Reflection;
using Antlr4.Runtime;
using FortallCompiler.Antlr;
using FortallCompiler.Ast;

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

        (ProgramNode? ast, bool success, List<Diagnostic> diagnostics) = SyntaticAnalyzer.Parse(s);

        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            return;
        }
        
        // prossegue com o pipeline
        // analise semantica agora, ver se faz sentido o codigo gerado
        // gerar tabelas etc
    }
}