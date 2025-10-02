using System.Text;
using FortallCompiler.Ast;
using FortallCompiler.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Steps;

public class DotnetGenerator {

    private ILProgram program = null!;
    private string projectName = null!; 
    private bool createExe;
    private bool readStringRequested;
    private bool readIntRequested;
    private bool readBoolRequested;
    private StreamWriter sw = null!;
    
    private Dictionary<string, Type> functionsReturnTypeMap = [];
    
    public void Generate(ILProgram program, Stream outputStream, string name, bool createExe) {
        this.program = program;
        this.createExe = createExe;
        sw = new StreamWriter(outputStream, leaveOpen: true);
        projectName = name;
        WriteHeader();
        WriteClass();
        sw.Dispose();
    }

    private void WriteHeader() {
        sw.WriteLine(".assembly extern System.Private.CoreLib {}");
        // sw.WriteLine(".assembly extern System.Console {}");
        // sw.WriteLine(".assembly extern mscorlib {}");
        sw.WriteLine($".assembly {projectName} {{}}");
        sw.WriteLine($".module {projectName}.{(createExe ? "exe" : "dll")}");
    }

    private void WriteClass() {
        sw.WriteLine($".class public auto ansi beforefieldinit {projectName}.Program extends [System.Private.CoreLib]System.Object {{");
        
        // create fields
        sw.WriteLine();
        sw.WriteLine("  // Fields");
        foreach (ILGlobalVariable field in program.Globals) {
            sw.Write("  .field public static ");
            sw.Write(TypeToILType(field.Type));
            sw.Write(" ");
            sw.WriteLine(field.Name);
        }
        sw.WriteLine();

        // create functions
        program.Functions.ForEach(x => functionsReturnTypeMap[x.Name] = x.ReturnType);
        foreach(ILFunction function in program.Functions) {
            WriteFunction(function);
            sw.WriteLine();
        }
        
        // create read methods if needed
        CreateReadMethods();
        
        // create constructor
        sw.WriteLine();
        WriteConstructor();
        
        sw.WriteLine("}");
    }

    private void WriteFunction(ILFunction function) {
        sw.WriteLine($"  // Function {function.Name}");
        Type returnValue = function.ReturnType;

        bool isMain = function.Name == "main";
        sw.Write($"  .method {(isMain ? "public" : "private")} hidebysig static {TypeToILType(returnValue)} '{function.Name}'(");
        for(int i = 0; i < function.Parameters.Count; i++) {
            ILParameter param = function.Parameters[i];
            sw.WriteLine();
            sw.Write("    ");
            sw.WriteLine(TypeToILType(param.Type));
            sw.Write(' ');
            sw.Write($"'{param.Name}'");
            if(i < function.Parameters.Count - 1) {
                sw.Write(", ");
            }
        }
        sw.WriteLine(") cil managed");
        
        ParameterAllocator parameterAllocator = new();
        foreach (ILParameter parameter in function.Parameters) {
            parameterAllocator.Allocate(parameter);
        }
        
        sw.WriteLine("  {");
        if(isMain) {
            sw.WriteLine("    .entrypoint");
        }
        sw.WriteLine("    .maxstack 8");
        LocalsAllocator localsAllocator = new();
        List<ILVar> locals = function.Instructions.OfType<ILVar>().ToList();
        foreach (ILVar local in locals) {
            localsAllocator.Allocate(new ILAddress(local.Name, ILAddressType.Stack, local.Type));
        }
        List<ILAddress> temporaries = CollectTemporaries(function);
        foreach (ILAddress temporary in temporaries) {
            localsAllocator.Allocate(temporary);
        }
        sw.Write(localsAllocator.GenerateLocalsText());
        
        sw.WriteLine();
        sw.WriteLine("    // ========== Instructions ==========");
        foreach(ILInstruction instruction in function.Instructions) {
            sw.WriteLine($"    // FIL: {instruction}");
            switch (instruction) {
                case ILBinaryOp binaryOp:
                    Generate(binaryOp, localsAllocator, parameterAllocator);
                    break;
                case ILCall call:
                    Generate(call, localsAllocator, parameterAllocator);
                    break;
                case ILGoto @goto:
                    Generate(@goto);
                    break;
                case ILIfGoto ifGoto:
                    Generate(ifGoto, localsAllocator, parameterAllocator);
                    break;
                case ILLabel label:
                    Generate(label);
                    break;
                case ILLoad load:
                    Generate(load, localsAllocator, parameterAllocator);
                    break;
                case ILLoadPtr loadPtr:
                    Generate(loadPtr, localsAllocator, parameterAllocator);
                    break;
                case ILMove move:
                    Generate(move, localsAllocator, parameterAllocator);
                    break;
                case ILRead read:
                    Generate(read, localsAllocator, parameterAllocator);
                    break;
                case ILReturn @return:
                    Generate(@return, localsAllocator, parameterAllocator);
                    break;
                case ILWrite write:
                    Generate(write, localsAllocator, parameterAllocator);
                    break;
                case ILUnaryOp unaryOp:
                    Generate(unaryOp, localsAllocator, parameterAllocator);
                    break;
                case ILVar var:
                    // No code generation needed for variable declaration
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        sw.WriteLine("  }");
    }

    private void WriteConstructor() {
        if (program.Globals.Count == 0) {
            return;
        }
        
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
                    sw.WriteLine($"    stsfld int32 {projectName}.Program::{global.Name}");
                    break;
                case Type.String:
                    sw.WriteLine($"    ldstr \"{(string)global.Value}\"");
                    sw.WriteLine($"    stsfld string {projectName}.Program::{global.Name}");
                    break;
                case Type.Boolean:
                    sw.WriteLine($"    ldc.i4.{((bool)global.Value ? 1 : 0)}");
                    sw.WriteLine($"    stsfld bool {projectName}.Program::{global.Name}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        sw.WriteLine("    ret");
        sw.WriteLine("  }");
    }

    #region Instruction Generation
    
    private void Generate(ILBinaryOp binaryOp, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        // load operands onto the stack
        // left
        sw.WriteLine(Load(binaryOp.Left, localsAllocator, parameterAllocator));
        sw.WriteLine(Load(binaryOp.Right, localsAllocator, parameterAllocator));
        
        // operation
        switch (binaryOp.Op) {
            case BinaryOperationType.Addition:
                sw.WriteLine("    add");
                break;
            case BinaryOperationType.Subtraction:
                sw.WriteLine("    sub");
                break;
            case BinaryOperationType.Multiplication:
                sw.WriteLine("    mul");
                break;
            case BinaryOperationType.Division:
                sw.WriteLine("    div");
                break;
            case BinaryOperationType.Equals:
                sw.WriteLine("    ceq");
                break;
            case BinaryOperationType.NotEquals:
                sw.WriteLine("    ceq");
                sw.WriteLine("    ldc.i4.0");
                sw.WriteLine("    ceq");
                break;
            case BinaryOperationType.LessThan:
                sw.WriteLine("    clt");
                break;
            case BinaryOperationType.LessEqualThan:
                sw.WriteLine("    cgt");
                sw.WriteLine("    ldc.i4.0");
                sw.WriteLine("    ceq");
                break;
            case BinaryOperationType.GreaterThan:
                sw.WriteLine("    cgt");
                break;
            case BinaryOperationType.GreaterEqualThan:
                sw.WriteLine("    clt");
                sw.WriteLine("    ldc.i4.0");
                sw.WriteLine("    ceq");
                break;
            case BinaryOperationType.And:
                sw.WriteLine("    and");
                break;
            case BinaryOperationType.Or:
                sw.WriteLine("    or");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        // store result
        sw.WriteLine(Store(binaryOp.Dest, localsAllocator, parameterAllocator));
    }

    private void Generate(ILCall call, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        for(int i=0; i < call.Arguments.Count; i++) {
            ILAddress arg = call.Arguments[i];
            sw.WriteLine(Load(arg, localsAllocator, parameterAllocator));
        }
        
        Type returnType = functionsReturnTypeMap[call.FunctionName];
        sw.Write($"    call {TypeToILType(returnType)} {projectName}.Program::{call.FunctionName}(");
        for(int i=0; i < call.Arguments.Count; i++) {
            ILAddress arg = call.Arguments[i];
            sw.Write(TypeToILType(arg.Type));
            if(i < call.Arguments.Count - 1) {
                sw.Write(", ");
            }
        }
        sw.WriteLine(")");
        
        if (call.Dest is not null && returnType != Type.Void) {
            sw.WriteLine(Load(call.Dest, localsAllocator, parameterAllocator));
        }
    }

    private void Generate(ILGoto @goto) {
        sw.WriteLine($"    br.s {@goto.Label}");
    }

    private void Generate(ILIfGoto ifGoto, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        sw.WriteLine(Load(ifGoto.Condition, localsAllocator, parameterAllocator));
        sw.WriteLine($"    brtrue.s {ifGoto.Label}");
    }

    private void Generate(ILLabel label) {
        sw.WriteLine($"    {label.Name}:");
    }

    private void Generate(ILLoad load, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        if (load.Value is int i) {
            // load integer constant onto stack
            if (i >= -1 && i <= 8) {
                sw.WriteLine($"    ldc.i4.{i}");
            }
            else {
                sw.WriteLine($"    ldc.i4 {i}");
            }

            // store into destination
            sw.WriteLine(Store(load.Dest, localsAllocator, parameterAllocator));
        }else if (load.Value is bool b) {
            // load bool onto stack
            sw.WriteLine($"    ldc.i4.{(b ? 1 : 0)}");
            // store into destination
            sw.WriteLine(Store(load.Dest, localsAllocator, parameterAllocator));
        }
        else {
            throw new NotSupportedException($"Type {load.Value.GetType()} not supported");
        }
    }
    
    private void Generate(ILLoadPtr loadPtr, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        sw.WriteLine($"    ldsfld string {projectName}.Program::{loadPtr.Src.Name}");
        sw.WriteLine(Store(loadPtr.Dest, localsAllocator, parameterAllocator));
    }
    
    private void Generate(ILMove move, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        // load source onto stack
        sw.WriteLine(Load(move.Src, localsAllocator, parameterAllocator));

        // store into destination
        sw.WriteLine(Store(move.Dest, localsAllocator, parameterAllocator));
    }
    
    private void Generate(ILRead read, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        string type = read.ReadType switch {
            Type.String => "String",
            Type.Integer => "Int",
            Type.Boolean => "Bool",
            _ => throw new NotSupportedException($"Read type {read.ReadType} not supported")
        };
        switch (read.ReadType) {
            case Type.String: readStringRequested = true; break;
            case Type.Boolean: readBoolRequested = true; break;
            case Type.Integer: readIntRequested = true;break;
        }
        sw.WriteLine($"    call {TypeToILType(read.ReadType)} {projectName}.Program::Read{type}()");

        sw.WriteLine(Store(read.Dest, localsAllocator, parameterAllocator));
    }
    
    private void Generate(ILReturn @return, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        if (@return.Value is not null && @return.Value.Type != Type.Void) {
            sw.WriteLine(Load(@return.Value, localsAllocator, parameterAllocator));
        }
        sw.WriteLine("    ret");
    }
    
    private void Generate(ILWrite write, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        sw.WriteLine(Load(write.Src, localsAllocator, parameterAllocator));
        sw.WriteLine($"    call void [System.Console]System.Console::Write({TypeToILType(write.WriteType)})");
        sw.WriteLine("    nop");
    }
    
    private void Generate(ILUnaryOp unaryOp, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        sw.WriteLine(Load(unaryOp.Operand, localsAllocator, parameterAllocator));
        switch (unaryOp.Op) {
            case UnaryOperationType.Not:
                sw.WriteLine("    ldc.i4.0");
                sw.WriteLine("    ceq");
                break;
            default:
                throw new NotSupportedException($"Unary op {unaryOp.Op} not supported");
        }
        sw.WriteLine(Store(unaryOp.Dest, localsAllocator, parameterAllocator));
    }

    #endregion
    
    private void CreateReadMethods() {
        if (readStringRequested) {
            const string readStringMethod = 
                """
                  // Method to read a string from console
                  .method private hidebysig static string ReadString() cil managed
                  {
                    .maxstack 2
                    call         string [System.Console]System.Console::ReadLine()
                    dup
                    brtrue.s fim
                    pop
                    ldstr       ""
                    ret
                  }
                """;
            sw.WriteLine(readStringMethod);
        }

        if (readIntRequested) {
            const string readIntMethod = 
                """
                  // Method to read an integer from console
                  .method private hidebysig static int32 ReadInt() cil managed
                  {
                    .maxstack 2
                    .locals init ([0] int32 'value')
                    
                    call         string [System.Console]System.Console::ReadLine()
                    dup
                    brtrue.s parse
                    pop
                    ldc.i4.0
                    ret // return 0 if input is null
                    parse:
                    ldloca.s 'value'
                    call bool [mscorlib]System.Int32::TryParse(string, int32&)
                    brtrue.s fim
                    ldc.i4.0 // if false, return 0
                    ret
                    fim:
                    ldloc.0 // return parsed value
                    ret
                  }
                """;
            sw.WriteLine(readIntMethod);
        }

        if (readBoolRequested) {
            const string readBoolMethod = 
                """
                  // Method to read a boolean from console
                  .method private hidebysig static bool ReadBool() cil managed
                  {
                    .maxstack 2
                    .locals init ([0] bool 'value')
                    
                    call         string [System.Console]System.Console::ReadLine()
                    dup
                    brtrue.s parse
                    pop
                    ldc.i4.0
                    ret // return false if input is null
                    parse:
                    ldloca.s 'value'
                    call bool [mscorlib]System.Boolean::TryParse(string, bool&)
                    brtrue.s fim
                    ldc.i4.0 // if false, return false
                    ret
                    fim:
                    ldloc.0 // return parsed value
                    ret
                  }
                """;
            sw.WriteLine(readBoolMethod);
        }
    }

    private class LocalsAllocator {
        
        private struct Local {
            public string Name;
            public Type Type;
            public bool Temporary;
        }
        
        private readonly List<Local> locals = [];
        
        public void Allocate(ILAddress address) {
            if (address.AddressType == ILAddressType.Global) {
                throw new NotSupportedException("Cant allocate a global variable as local");
            }

            if (address.Type == Type.Void) {
                return;
            }
            locals.Add(
                new Local() {
                    Name = address.Name,
                    Type = address.Type,
                    Temporary = address.AddressType == ILAddressType.Temporary
                }
            );
        }

        public int GetIndex(ILAddress address) {
            for (int i = 0; i < locals.Count; i++) {
                if (locals[i].Name == address.Name) {
                    return i;
                }
            }
            throw new KeyNotFoundException($"Local variable {address.Name} not found");
        }
        
        public string GenerateLocalsText() {
            if (locals.Count == 0) {
                return string.Empty;
            }
            StringBuilder sb = new();
            sb.Append("    .locals init (");
            for (int i = 0; i < locals.Count; i++) {
                sb.AppendLine();
                sb.Append($"      [{i}] ");
                sb.Append(TypeToILType(locals[i].Type));

                if (!locals[i].Temporary) {
                    sb.Append($" '{locals[i].Name}'");
                }
                if (i < locals.Count - 1) {
                    sb.Append(',');
                }

                if (locals[i].Temporary) {
                    sb.Append($" // Temporary '{locals[i].Name}'");
                }
            }
            sb.AppendLine();
            sb.AppendLine("    )");
            return sb.ToString();
        }
    }

    private class ParameterAllocator {
        private readonly List<ILParameter> parameters = [];
        
        public void Allocate(ILParameter parameter) {
            parameters.Add(parameter);
        }
        
        public int GetIndex(string name) {
            for (int i = 0; i < parameters.Count; i++) {
                if (parameters[i].Name == name) {
                    return i;
                }
            }
            throw new KeyNotFoundException($"Parameter {name} not found");
        }
        
        public string GetLoadText(string name) {
            int index = GetIndex(name);
            if (index <= 3) return "ldarg." + index;
            if (index <= 255) return  "ldarg.s " + index;
            return "ldarg " + index;
        }
        
        public string GetStoreText(string name) {
            int index = GetIndex(name);
            if (index <= 3) return "starg." + index;
            if (index <= 255) return  "starg.s " + index;
            return "starg " + index;
        }
    }

    private List<ILAddress> CollectTemporaries(ILFunction function) {
        HashSet<ILAddress> temporaries = [];
        foreach (ILInstruction instruction in function.Instructions) {
            switch (instruction) {
                case ILBinaryOp binaryOp:
                    if (binaryOp.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(binaryOp.Dest);
                    }

                    break;
                case ILCall call:
                    if (call.Dest?.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(call.Dest);
                    }

                    break;
                case ILLoad load:
                    if (load.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(load.Dest);
                    }

                    break;
                case ILLoadPtr loadptr:
                    if (loadptr.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(loadptr.Dest);
                    }

                    break;
                case ILMove move:
                    if (move.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(move.Dest);
                    }

                    break;
                case ILRead read:
                    if (read.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(read.Dest);
                    }

                    break;
                case ILUnaryOp unaryOp:
                    if (unaryOp.Dest.AddressType == ILAddressType.Temporary) {
                        temporaries.Add(unaryOp.Dest);
                    }

                    break;
            }
        }

        return temporaries.ToList();
    }
    
    private static string TypeToILType(Type type) {
        return type switch {
            Type.Integer => "int32",
            Type.Boolean => "bool",
            Type.String => "string",
            Type.Void => "void",
            _ => throw new NotSupportedException($"Type {type} not implemented")
        };
    }

    private string Store(ILAddress address, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        return address.AddressType switch {
            ILAddressType.Global => $"stsfld {TypeToILType(address.Type)} {projectName}.Program::{address.Name}",
            ILAddressType.Stack or ILAddressType.Temporary => StoreLocal(localsAllocator.GetIndex(address)),
            ILAddressType.Parameter => parameterAllocator.GetStoreText(address.Name),
            _ => throw new NotSupportedException($"Address type {address.AddressType} not supported")
        };
    }
    
    private string Load(ILAddress address, LocalsAllocator localsAllocator, ParameterAllocator parameterAllocator) {
        return address.AddressType switch {
            ILAddressType.Global => $"ldsfld {TypeToILType(address.Type)} {projectName}.Program::{address.Name}",
            ILAddressType.Stack or ILAddressType.Temporary => LoadLocal(localsAllocator.GetIndex(address)),
            ILAddressType.Parameter => parameterAllocator.GetLoadText(address.Name),
            _ => throw new NotSupportedException($"Address type {address.AddressType} not supported")
        };
    }

    private string StoreLocal(int index) {
        if(index <= 3) return "stloc." + index;
        if(index <= 255) return  "stloc.s " + index;
        return "stloc " + index;
    }
    
    private string LoadLocal(int index) {
        if(index <= 3) return "ldloc." + index;
        if(index <= 255) return  "ldloc.s " + index;
        return "ldloc " + index;
    }
}