namespace FortallCompiler;

public class Compiler {
    
    public void Compile(string path) {
        string linkerScriptPath = "linker.ld";
        string outputPath = "";
        string args =
            $"--target=mips-linux-gnu -O0 -fno-pic -mno-abicalls -nostartfiles -T \"{linkerScriptPath}\" -nostdlib -fuse-ld=lld -static \"{path}\"  -o \"{outputPath}\"";
    }
}