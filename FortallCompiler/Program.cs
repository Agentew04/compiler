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

        Console.WriteLine("Realizando a analise sintatica...");
        SyntaticAnalyzer syntaticAnalyzer = new();
        (ProgramNode? ast, bool success, List<Diagnostic> diagnostics) = syntaticAnalyzer.Analyze(s);

        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            Console.WriteLine("Analise sintatica falhou :(");
            Console.WriteLine("Diagnosticos: ");
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"\tLinha {diagnostic.Line}, Coluna {diagnostic.Column}: {diagnostic.Message}");
            }
            return;
        }

        if (ast is null) {
            Console.WriteLine("Erro inesperado: AST retornou nulo mas teve sucesso e nao teve diagnosticos :(");
            return;
        }
        Console.WriteLine("Analise sintatica bem sucedida!");
        
        // prossegue com o pipeline
        // analise semantica agora, ver se faz sentido o codigo gerado
        // gerar tabelas etc
        Console.WriteLine("Realizando a analise semantica...");
        SemanticAnalyzer semanticAnalyzer = new();
        (success, diagnostics) = semanticAnalyzer.Analyze(ast);
        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            Console.WriteLine("Analise semantica falhou :(");
            Console.WriteLine("Diagnosticos: ");
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"\tLinha {diagnostic.Line}, Coluna {diagnostic.Column}: {diagnostic.Message}");
            }
            return;
        }
        Console.WriteLine("Analise semantica bem sucedida!");
    }
}