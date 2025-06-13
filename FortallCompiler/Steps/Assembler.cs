using System.Diagnostics;
using System.Reflection;

namespace FortallCompiler;

public class Assembler {
    
    public bool Compile(string path) {
        string tempPath = Path.GetTempPath();
        string linkerPath = Path.Combine(tempPath, "linker.ld");
        FileStream fs = File.Open(linkerPath, FileMode.OpenOrCreate);
        Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortallCompiler.linker.ld")!;
        s.CopyTo(fs);
        s.Dispose();
        fs.SetLength(fs.Position);
        fs.Dispose();
        
        string outputPath = Path.ChangeExtension(path, ".exe");
        string args =
            $"--target=mips-linux-gnu -O0 -fno-pic -mno-abicalls -nostartfiles -T \"{linkerPath}\" -nostdlib -fuse-ld=lld -static \"{path}\"  -o \"{outputPath}\"";

        Console.WriteLine("Args: " + args);
        ProcessStartInfo processStartInfo = new() {
            FileName = "clang",
            Arguments = args
        };
        Process? process = Process.Start(processStartInfo);
        if (process is null) {
            return false;
        }
        process.WaitForExit();

        if (process.ExitCode != 0) {
            return false;
        }
        return true;
    }
}