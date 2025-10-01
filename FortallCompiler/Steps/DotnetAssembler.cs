using System.Diagnostics;

namespace FortallCompiler.Steps;

public class DotnetAssembler {
    public bool Compile(string ilPath, out string outputPath) {
        outputPath = Path.ChangeExtension(ilPath, ".exe");
        ProcessStartInfo ilasmProcInfo = new() {
            FileName = "ilasm.exe",
            Arguments = $"\"{ilPath}\" /EXE /OUTPUT=\"{outputPath}\"",
        };
        Process ilasmProc = Process.Start(ilasmProcInfo)!;
        ilasmProc.WaitForExit();
        
        // deps.json and runtimeconfig.json
        File.WriteAllText(Path.ChangeExtension(outputPath, ".deps.json"), depsJson);
        File.WriteAllText(Path.ChangeExtension(outputPath, ".runtimeconfig.json"), runtimeConfigJson);
        
        if (ilasmProc.ExitCode == 0) return true;
        outputPath = string.Empty;
        return false;
    }

    private string depsJson =
        """
        {
          "runtimeTarget": {
            "name": ".NETCoreApp,Version=v9.0",
            "signature": ""
          },
          "compilationOptions": {},
          "targets": {
            ".NETCoreApp,Version=v9.0": {
              "FortallProgram/1.0.0": {
                "runtime": {
                  "FortallProgram.dll": {}
                }
              }
            }
          },
          "libraries": {
            "FortallProgram/1.0.0": {
              "type": "project",
              "serviceable": false,
              "sha512": ""
            }
          }
        }
        """;
    
    private string runtimeConfigJson =
        """
        {
          "runtimeOptions": {
            "tfm": "net9.0",
            "framework": {
              "name": "Microsoft.NETCore.App",
              "version": "9.0.0"
            },
            "configProperties": {
              "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
            }
          }
        }
        """;
}