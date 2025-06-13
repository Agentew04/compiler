using System.Diagnostics;
using System.Reflection;
using FortallCompiler.Ast;
using FortallCompiler.IL;

namespace FortallCompiler;

class Program
{
    private static void Main(string[] args)
    {
        Stream? s;
        string path;
        if (args.Length > 0 && File.Exists(args[0]))
        {
            path = args[0];
            Console.WriteLine($"Utilizando arquivo \"{Path.GetFileName(path)}\"");
            s = new FileStream(path, FileMode.Open, FileAccess.Read);
        }
        else
        {
            path = "./test.all";
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

        if (!SyntaticAnalysis(s, sw, ref totalTime, out ProgramNode? ast))
        {
            return;
        }

        if (!SemanticAnalysis(ast!, sw, ref totalTime))
        {
            return;
        }
        
        string ilPath = Path.ChangeExtension(path, ".il");
        FileStream ilFs = File.Open(ilPath, FileMode.OpenOrCreate, FileAccess.Write);
        IlGeneration(ast!, sw, ref totalTime, ilFs, out ILProgram ilProgram);
        ilFs.Close();
        
        string asmPath = Path.ChangeExtension(path, ".s");
        FileStream asmFs = File.Open(asmPath, FileMode.OpenOrCreate, FileAccess.Write);
        AssemblyGeneration(ilProgram, sw, ref totalTime, asmFs);
        
        Compilation(asmPath, sw, ref totalTime);

        Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
        Console.WriteLine("Executar? (S/n)");
    }


    private static bool SyntaticAnalysis(Stream inputStream, Stopwatch sw, ref double totalTime, out ProgramNode? ast)
    {
        Console.WriteLine("Realizando a analise sintatica...");
        SyntaticAnalyzer syntaticAnalyzer = new();
        sw.Start();
        (ast, bool success, List<Diagnostic> diagnostics) = syntaticAnalyzer.Analyze(inputStream);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        
        // libera arquivo original
        inputStream.Dispose();

        if (!success || diagnostics.Count > 0) {
            // mostra erros pro usuario
            Console.WriteLine($"Analise sintatica falhou em {sw.Elapsed.TotalMilliseconds}ms :(");
            Console.WriteLine("Diagnosticos: ");
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"\tLinha {diagnostic.Line}, Coluna {diagnostic.Column}: {diagnostic.Message}");
            }
            return false;
        }

        if (ast is null) {
            Console.WriteLine("Erro inesperado: AST retornou nulo mas teve sucesso e nao teve diagnosticos :(");
            return false;
        }
        Console.WriteLine($"Analise sintatica bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
        return true;
    }

    private static bool SemanticAnalysis(ProgramNode ast, Stopwatch sw, ref double totalTime)
    {
        Console.WriteLine("Realizando a analise semantica...");
        SemanticAnalyzer semanticAnalyzer = new();
        sw.Restart();
        (bool success, var diagnostics) = semanticAnalyzer.Analyze(ast);
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
            return false;
        }
        Console.WriteLine($"Encontrei {ast.StringLiterals.Count} string literals!");
        Console.WriteLine($"Analise semantica bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
        return true;
    }
    
    private static void IlGeneration(ProgramNode ast, Stopwatch sw, ref double totalTime, Stream outputStream, out ILProgram ilProgram)
    {
        Console.WriteLine("Comecando a geracao de codigo intermediario...");
        IlGenerator ilGenerator = new();
        sw.Restart();
        ilProgram = ilGenerator.GenerateIlCode(ast);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Geracao de codigo bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        
        Console.WriteLine("Escrevendo codigo intermediario para o disco...");
        sw.Restart();
        using StreamWriter writer = new(outputStream, leaveOpen: true);
        writer.Write(ilProgram.ToString());
        writer.Flush();
        outputStream.Flush();
        outputStream.SetLength(outputStream.Position);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Escrito codigo intermediario no disco em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
    }
    
    private static void AssemblyGeneration(ILProgram ilProgram, Stopwatch sw, ref double totalTime, Stream outputStream)
    {
        using MemoryStream ms = new();
        Console.WriteLine("Traduzindo codigo intermediario assembly MIPS...");
        MipsGenerator mipsGenerator = new();
        sw.Restart();
        mipsGenerator.Generate(ilProgram, ms);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Traducao para assembly bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        
        Console.WriteLine("Escrevendo assembly no disco...");
        sw.Restart();
        ms.Seek(0, SeekOrigin.Begin);
        ms.CopyTo(outputStream);
        outputStream.Flush();
        outputStream.SetLength(outputStream.Position);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Escrito assembly no disco em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
    }

    private static void Compilation(string path, Stopwatch sw, ref double totalTime)
    {
        Console.WriteLine("Comecando a compilacao com ferramenta externa...");
        sw.Restart();
        // TODO: chamar clang e compilar
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Compilacao bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
    }
}