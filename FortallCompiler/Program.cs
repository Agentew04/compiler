using System.Diagnostics;
using System.Reflection;
using FortallCompiler.Ast;

namespace FortallCompiler;

class Program
{
    static void Main(string[] args)
    {
        Stream? s;
        if (args.Length > 0 && File.Exists(args[0]))
        {
            string path = args[0];
            Console.WriteLine($"Utilizando arquivo \"{Path.GetFileName(path)}\"");
            s = new FileStream(path, FileMode.Open, FileAccess.Read);
        }
        else
        {
            Console.WriteLine("Utilizando arquivo de teste interno: test.all");
            s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortallCompiler.test.all");
        }

        if (s is null)
        {
            Console.WriteLine("nao encontrei nenhum arquivo de teste possivel");
            return;
        }
        
        Stopwatch sw = new();
        double totalTime = 0;

        Console.WriteLine("Realizando a analise sintatica...");
        SyntaticAnalyzer syntaticAnalyzer = new();
        sw.Start();
        (ProgramNode? ast, bool success, List<Diagnostic> diagnostics) = syntaticAnalyzer.Analyze(s);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        
        // libera arquivo original
        s.Dispose();

        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            Console.WriteLine($"Analise sintatica falhou em {sw.Elapsed.TotalMilliseconds}ms :(");
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
        Console.WriteLine($"Analise sintatica bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
        
        // prossegue com o pipeline
        // analise semantica agora, ver se faz sentido o codigo gerado
        // gerar tabelas etc
        Console.WriteLine("Realizando a analise semantica...");
        SemanticAnalyzer semanticAnalyzer = new();
        sw.Restart();
        (success, diagnostics) = semanticAnalyzer.Analyze(ast);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            Console.WriteLine($"Analise semantica falhou em {sw.Elapsed.TotalMilliseconds}ms :(");
            Console.WriteLine("Diagnosticos: ");
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"\tLinha {diagnostic.Line}, Coluna {diagnostic.Column}: {diagnostic.Message}");
            }
            return;
        }
        Console.WriteLine($"Analise semantica bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();

        Console.WriteLine("Comecando a geracao de codigo intermediario...");
        CodeGenerator codeGenerator = new();
        sw.Restart();
        codeGenerator.GenerateIlCode(ast);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Geracao de codigo bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();

        Console.WriteLine("Comecando a compilacao com ferramenta externa...");
        sw.Restart();
        // TODO: chamar clang e compilar
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Compilacao bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();

        Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
        Console.WriteLine("Executar? (S/n)");
    }
}