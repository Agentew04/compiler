using System.Diagnostics;
using System.Reflection;
using FortallCompiler.Ast;
using FortallCompiler.IL;
using FortallCompiler.Steps;

namespace FortallCompiler;

public static class Program
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
            Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
            return;
        }

        if (!SemanticAnalysis(ast!, sw, ref totalTime))
        {
            Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
            return;
        }
        
        string ilPath = Path.ChangeExtension(path, ".fil");
        FileStream ilFs = File.Open(ilPath, FileMode.OpenOrCreate, FileAccess.Write);
        IlGeneration(ast!, sw, ref totalTime, ilFs, out ILProgram ilProgram);
        ilFs.Close();

        Console.WriteLine("Qual plataforma de destino?");
        Console.WriteLine("\t1. MIPS");
        Console.WriteLine("\t2. .NET IL");
        string targetFrameworkInput = Console.ReadLine() ?? "";
        if(!int.TryParse(targetFrameworkInput, out int targetFramework) || targetFramework < 1 || targetFramework > 2) {
            Console.WriteLine("Plataforma invalida, abortando...");
            return;
        }

        if (targetFramework == 1) {
            // MIPS
            MipsFlow(path, ilProgram, sw, ref totalTime);
        }
        else if(targetFramework == 2) {
            // .NET IL
            DotnetFlow(path, ilProgram, sw, ref totalTime);
        }
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

    private static void MipsFlow(string path, ILProgram ilProgram, Stopwatch sw, ref double totalTime) {
        string asmPath = Path.ChangeExtension(path, ".s");
        FileStream asmFs = File.Open(asmPath, FileMode.OpenOrCreate, FileAccess.Write);
        if (!MipsAssemblyGeneration(ilProgram, sw, ref totalTime, asmFs)) {
            return;            
        }
        asmFs.Dispose();

        if (!MipsAssemble(asmPath, sw, ref totalTime, out string outputPath)) {
            Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
            return;            
        }

        sw.Stop();
        Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
        Console.WriteLine("Executar? (S/N)");
        string input = Console.ReadLine() ?? "S";
        if (input is "S" or "s") {
            MipsRun(outputPath);
        }
    }
    
    private static bool MipsAssemblyGeneration(ILProgram ilProgram, Stopwatch sw, ref double totalTime, Stream outputStream)
    {
        using MemoryStream ms = new();
        Console.WriteLine("Traduzindo codigo intermediario assembly MIPS...");
        MipsGenerator mipsGenerator = new();
        sw.Restart();
        try {
            mipsGenerator.Generate(ilProgram, ms);
        }
        catch (Exception e) {
            // erro, printa o resto
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return false;
        }

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
        return true;
    }

    private static bool MipsAssemble(string path, Stopwatch sw, ref double totalTime, out string outputPath)
    {
        Console.WriteLine("Comecando a montagem com ferramenta externa...");
        MipsAssembler mipsAssembler = new();
        sw.Restart();
        bool success = mipsAssembler.Compile(path, out outputPath);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        if (!success)
        {
            Console.WriteLine("Ocorreu um erro na montagem :(");
            return false;
        }

        Console.WriteLine($"Montagem bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        
        Console.WriteLine("Deseja ver os headers do ELF e o DISSASSEMBLY? (S/N)");
        string? input = Console.ReadLine();
        if (input is not null && input.ToUpper() == "S") {
            mipsAssembler.ShowMetrics(outputPath);
        }
        Console.WriteLine();
        return true;
    }
    
    private static void MipsRun(string path) {
        Runner runner = new();
        runner.Run(path);
    }
    
    private static void DotnetFlow(string path, ILProgram ilProgram, Stopwatch sw, ref double totalTime) {
        string ilPath = Path.ChangeExtension(path, ".il");
        string name = Path.GetFileNameWithoutExtension(path);
        FileStream ilFs = File.Open(ilPath, FileMode.OpenOrCreate, FileAccess.Write);
        if (!DotnetGeneration(ilProgram, sw, ref totalTime, ilFs, name)) {
            ilFs.Dispose();
            return;
        }
        ilFs.Dispose();

        if (!DotnetAssemble(ilPath, sw, ref totalTime, out string outputPath, name)) {
            return;
        }
        
        sw.Stop();
        Console.WriteLine("Tempo total de execucao: " + totalTime + "ms");
        Console.WriteLine("Executar? (S/N)");
        string input = Console.ReadLine() ?? "S";
        if (input is "S" or "s") {
            DotnetRun(outputPath);
        }
    }

    private static bool DotnetGeneration(ILProgram ilProgram, Stopwatch sw, ref double totalTime, Stream outputStream, string name) {
        using MemoryStream ms = new();
        Console.WriteLine("Traduzindo codigo intermediario para .NET IL...");
        DotnetGenerator generator = new();
        sw.Restart();
        try {
            generator.Generate(ilProgram, ms, name);
        }
        catch (Exception e) {
            // erro, printa o resto
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return false;
        }

        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Traducao para .NET IL bem sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        
        Console.WriteLine("Escrevendo .NET IL no disco...");
        sw.Restart();
        ms.Seek(0, SeekOrigin.Begin);
        ms.CopyTo(outputStream);
        outputStream.Flush();
        outputStream.SetLength(outputStream.Position);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Escrito .NET IL no disco em {sw.Elapsed.TotalMilliseconds}ms!");
        Console.WriteLine();
        return true;
    }

    private static bool DotnetAssemble(string ilPath, Stopwatch sw, ref double totalTime, out string outputPath, string name) {
        Console.WriteLine("Comecando a montagem com ILASM...");
        DotnetAssembler assembler = new();
        sw.Restart();
        bool success = assembler.Assemble(ilPath, out outputPath, name);
        sw.Stop();
        totalTime += sw.Elapsed.TotalMilliseconds;
        if (!success)
        {
            Console.WriteLine("Ocorreu um erro na montagem :(");
            return false;
        }

        Console.WriteLine($"Montagem para .NET DLL sucedida em {sw.Elapsed.TotalMilliseconds}ms!");
        return true;
    }
    
    
    
    private static void DotnetRun(string path) {
        ProcessStartInfo executeStartInfo = new() {
            FileName = path,
        };
        Console.WriteLine($"Executando {path}");
        Process? executeProc = Process.Start(executeStartInfo);
        executeProc?.WaitForExit();
        Console.WriteLine("Exit code: " + executeProc?.ExitCode);
    }
}