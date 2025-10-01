using FortallCompiler.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Steps;

public class DotnetGenerator {

    private ILProgram program;
    
    public void Generate(ILProgram program, Stream outputStream) {
        this.program = program;
        using StreamWriter sw = new(outputStream, leaveOpen: true);
        WriteHeader("FortallProgram",sw);
        WriteClass(sw);
    }

    private void WriteHeader(string assemblyName, StreamWriter sw) {
        sw.WriteLine(".assembly extern System.Runtime {");
        sw.WriteLine("  .ver 9:0:0:0");
        sw.WriteLine("}");
        sw.WriteLine(".assembly extern System.Console {");
        sw.WriteLine("  .ver 9:0:0:0");
        sw.WriteLine("}");
        sw.WriteLine($".assembly {assemblyName} {{}}");
        sw.WriteLine($".module {assemblyName}.exe");
    }

    private void WriteClass(StreamWriter sw) {
        sw.WriteLine(".class public auto ansi beforefieldinit FortallProgram.Program extends [System.Runtime]System.Object {");
        
        // create fields
        sw.WriteLine();
        sw.WriteLine("  // Fields");
        foreach (ILGlobalVariable field in program.Globals) {
            sw.Write("  .field public static ");
            sw.Write(field.Type switch {
                Type.Integer => "int32",
                Type.Boolean => "bool",
                Type.String => "string",
                _ => throw new NotSupportedException($"Type {field.Type} not implemented")
            });
            sw.Write(" ");
            sw.WriteLine(field.Name);
        }
        sw.WriteLine();

        // create functions
        foreach(var function in program.Functions) {
            WriteFunction(function, sw);
            sw.WriteLine();
        }
        
        // create constructor'
        WriteConstructor(sw);
        sw.WriteLine("}");
    }

    private void WriteFunction(ILFunction function, StreamWriter sw) {
        sw.WriteLine($"  // Function {function.Name}");
        sw.Write($"  .method private hidebysig static void '{function.Name}'(");
        for(int i = 0; i < function.Parameters.Count; i++) {
            ILParameter param = function.Parameters[i];
            sw.WriteLine();
            sw.Write(param.Type switch {
                Type.Integer => "    int32 ",
                Type.Boolean => "    bool ",
                Type.String => "    string ",
                _ => throw new NotSupportedException($"Type {param.Type} not implemented")
            });
            sw.Write(param.Name);
            if(i < function.Parameters.Count - 1) {
                sw.Write(", ");
            }
        }
        sw.WriteLine(") cil managed");
        sw.WriteLine("  {");
        if(function.Name == "main") {
            sw.WriteLine("    .entrypoint");
        }
        sw.WriteLine("    .maxstack 8");
        List<ILVar> locals = function.Instructions.OfType<ILVar>().ToList();
        if (locals.Count > 0) {
            sw.WriteLine("    .locals init (");
            for(int i=0; i < locals.Count; i++) {
                ILVar local = locals[i];
                sw.Write("      ");
                sw.Write($"[{i}]");
                sw.Write(local.Type switch {
                    Type.Integer => "int32 ",
                    Type.Boolean => "bool ",
                    Type.String => "string ",
                    _ => throw new NotSupportedException($"Type {local.Type} not implemented")
                });
                sw.Write(local.Name);
                if(i < locals.Count - 1) {
                    sw.WriteLine(",");
                } else {
                    sw.WriteLine();
                }
            }
        }
        
        sw.WriteLine();
        sw.WriteLine("    // Instructions Start");
        foreach(ILInstruction instruction in function.Instructions) {
            sw.WriteLine($"    // {instruction}");
            Generate(instruction, sw);
        }
        
        sw.WriteLine("    ret");
        sw.WriteLine("  }");
    }

    private void WriteConstructor(StreamWriter sw) {
        sw.WriteLine("  // Static constructor");
        sw.WriteLine("  .method private hidebysig static specialname rtspecialname void .cctor() cil managed");
        sw.WriteLine("  {");
        sw.WriteLine("    .maxstack 8");
        
        // field initialization
        sw.WriteLine();
        sw.WriteLine("    // Field initialization");
        foreach (ILGlobalVariable global in program.Globals) {
            if(global.Value is null) continue;
            switch (global.Type) {
                case Type.Integer:
                    if ((int)global.Value >= -1 && (int)global.Value <= 8) {
                        sw.WriteLine($"    ldc.i4.{(int)global.Value}");
                    }
                    else {
                        sw.WriteLine($"    ldc.i4 {(int)global.Value} ");
                    }
                    sw.WriteLine($"    stsfld int32 FortallProgram.Program::{global.Name}");
                    break;
                case Type.String:
                    sw.WriteLine($"    ldstr \"{(string)global.Value}\"");
                    sw.WriteLine($"    stsfld string FortallProgram.Program::{global.Name}");
                    break;
                case Type.Boolean:
                    sw.WriteLine($"    ldc.i4.{((bool)global.Value ? 1 : 0)}");
                    sw.WriteLine($"    stsfld bool FortallProgram.Program::{global.Name}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        // call object base constructor
        sw.WriteLine("    ret");
        
        sw.WriteLine("  }");
    }

    #region Instruction Generation

    private void Generate(ILInstruction instruction, StreamWriter sw) {
        switch (instruction) {
            case ILBinaryOp binaryOp:
                Generate(binaryOp, sw);
                break;
            case ILCall call:
                Generate(call, sw);
                break;
            case ILGoto @goto:
                Generate(@goto, sw);
                break;
            case ILIfGoto ifGoto:
                Generate(ifGoto, sw);
                break;
            case ILLabel label:
                Generate(label, sw);
                break;
            case ILLoad load:
                Generate(load, sw);
                break;
            case ILLoadPtr loadptr:
                Generate(loadptr, sw);
                break;
            case ILMove move:
                Generate(move, sw);
                break;
            case ILRead read:
                Generate(read, sw);
                break;
            case ILReturn @return:
                Generate(@return, sw);
                break;
            case ILWrite write:
                Generate(write, sw);
                break;
            case ILUnaryOp unaryOp:
                Generate(unaryOp, sw);
                break;
            case ILVar var:
                // No code generation needed for variable declaration
                break;
        }
    }

    private void Generate(ILBinaryOp binaryOp, StreamWriter sw) {
        
    }

    private void Generate(ILCall call, StreamWriter sw) {
        
    }

    private void Generate(ILGoto @goto, StreamWriter sw) {
        
    }

    private void Generate(ILIfGoto ifGoto, StreamWriter sw) {
        
    }

    private void Generate(ILLabel label, StreamWriter sw) {
        
    }

    private void Generate(ILLoad load, StreamWriter sw) {
        
    }
    
    private void Generate(ILLoadPtr laodptr, StreamWriter sw) {
        
    }
    
    private void Generate(ILMove move, StreamWriter sw) {
        
    }
    
    private void Generate(ILRead read, StreamWriter sw) {
        
    }
    
    private void Generate(ILReturn @return, StreamWriter sw) {
        
    }
    
    private void Generate(ILWrite write, StreamWriter sw) {
        
    }
    
    private void Generate(ILUnaryOp unaryOp, StreamWriter sw) {
        
    }

    #endregion
}