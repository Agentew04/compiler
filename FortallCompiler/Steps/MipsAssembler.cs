using System.Diagnostics;
using System.Reflection;

namespace FortallCompiler.Steps;

public class MipsAssembler {
    
    public bool Compile(string path, out string outputPath) {
        string tempPath = Path.GetTempPath();
        string linkerPath = Path.Combine(tempPath, "linker.ld");
        FileStream fs = File.Open(linkerPath, FileMode.OpenOrCreate);
        Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortallCompiler.linker.ld")!;
        s.CopyTo(fs);
        s.Dispose();
        fs.SetLength(fs.Position);
        fs.Dispose();
        
        outputPath = Path.ChangeExtension(path, ".exe");
        string args =
            $"--target=mips-linux-gnu -O0 -fno-pic -mno-abicalls -nostartfiles -T \"{linkerPath}\" -nostdlib -fuse-ld=lld -static \"{path}\"  -o \"{outputPath}\"";

        Console.WriteLine($"Args: clang.exe {args}");
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
            process.Close();
            return false;
        }
        process.Close();
        return true;
    }

    public void ShowMetrics(string path)
    {
        // headers
        Console.WriteLine("-=-=-=-=-=-=-=-=- HEADERS (READELF) -=-=-=-=-=-=-=-=-");
        ProcessStartInfo objdumpInfo = new() {
            FileName = "llvm-readelf",
            Arguments = $"\"{path}\" -header",
        };
        Process? readElfProc = Process.Start(objdumpInfo);
        if (readElfProc is not null) {
            readElfProc.WaitForExit();
        }
            
        Console.WriteLine("-=-=-=-=-=-=-=-=- DISASM (OBJDUMP) -=-=-=-=-=-=-=-=-");
        ProcessStartInfo objdumpDisasmInfo = new() {
            FileName = "llvm-objdump",
            Arguments = $"\"{path}\" -d",
        };
        Process? objdumpProc = Process.Start(objdumpDisasmInfo);
        if (objdumpProc is not null) {
            objdumpProc.WaitForExit();
        }
    }
}