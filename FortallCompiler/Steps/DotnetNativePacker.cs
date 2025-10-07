using System.Diagnostics;

namespace FortallCompiler.Steps;

public class DotnetNativePacker {
    public bool Pack(string folder, string dll, string name, out string exePath) {
        File.WriteAllText(Path.Combine(folder, $"{name}-native.csproj"), 
	        Csproj
				.Replace("{PROJECT}", dll)
				.Replace("{NAME}", name));
        File.WriteAllText(Path.Combine(folder, "Program.cs"),
	        ProgramCs
		        .Replace("{NAME}", name));

        exePath = Path.Combine($"{name}-native.exe");
        ProcessStartInfo startInfo = new ProcessStartInfo() {
			FileName = "dotnet",
			Arguments = $"publish -c Release {Path.Combine(folder, name+"-native.csproj")} -o \".\" -p:PublishSingleFile=true",
        };
        using Process? proc = Process.Start(startInfo);
        proc?.WaitForExit();
        
        if(proc?.ExitCode!=0) {
	        exePath = "";
	        return false;
		}
        
        Directory.Delete(folder, true);
        return true;
    }

    private const string Csproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net9.0</TargetFramework>
        		<PublishAot>true</PublishAot>
        		<RuntimeIdentifier>win-x64</RuntimeIdentifier>
            </PropertyGroup>
        	
            <ItemGroup>
        		<Reference Include="{NAME}">
        			<HintPath>../{PROJECT}</HintPath>
        			<Private>true</Private>
        		</Reference>
            </ItemGroup>
        </Project>
        """;

    private const string ProgramCs =
	    """
	    class Program {
	        static void Main() => {NAME}.Program.main();
	    }
	    """;
}
