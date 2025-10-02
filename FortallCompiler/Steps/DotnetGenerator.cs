using System.Text;
using FortallCompiler.Ast;
using FortallCompiler.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Steps;

public class DotnetGenerator {

    private ILProgram program;
    private string projectName; 
    private bool createExe;
    
    public void Generate(ILProgram program, Stream outputStream, string name, bool createExe) {
        this.program = program;
        using StreamWriter sw = new(outputStream, leaveOpen: true);
        projectName = name;
        WriteHeader(sw);
        WriteClass(sw);
    }

    private void WriteHeader(StreamWriter sw) {
        sw.WriteLine(".assembly extern System.Private.CoreLib {}");
        // sw.WriteLine(".assembly extern System.Console {}");
        // sw.WriteLine(".assembly extern mscorlib {}");
        sw.WriteLine($".assembly {projectName} {{}}");
        sw.WriteLine($".module {projectName}.{(createExe ? "exe" : "dll")}");
    }

    private void WriteClass(StreamWriter sw) {
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
        foreach(var function in program.Functions) {
            WriteFunction(function, sw);
            sw.WriteLine();
        }
        
        // create constructor
        WriteConstructor(sw);
        sw.WriteLine("}");
    }

    private void WriteFunction(ILFunction function, StreamWriter sw) {
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
                    Generate(binaryOp, sw, localsAllocator);
                    break;
                case ILCall call:
                    Generate(call, sw);
                    break;
                case ILGoto @goto:
                    Generate(@goto, sw);
                    break;
                case ILIfGoto ifGoto:
                    Generate(ifGoto, sw, localsAllocator);
                    break;
                case ILLabel label:
                    Generate(label, sw);
                    break;
                case ILLoad load:
                    Generate(load, sw, localsAllocator);
                    break;
                case ILLoadPtr loadPtr:
                    Generate(loadPtr, sw, localsAllocator);
                    break;
                case ILMove move:
                    Generate(move, sw, localsAllocator);
                    break;
                case ILRead read:
                    Generate(read, sw);
                    break;
                case ILReturn @return:
                    Generate(@return, sw, localsAllocator);
                    break;
                case ILWrite write:
                    Generate(write, sw, localsAllocator);
                    break;
                case ILUnaryOp unaryOp:
                    Generate(unaryOp, sw, localsAllocator);
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

    private void WriteConstructor(StreamWriter sw) {
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
        
        // call object base constructor
        sw.WriteLine("    ret");
        
        sw.WriteLine("  }");
    }

    #region Instruction Generation
    
    private void Generate(ILBinaryOp binaryOp, StreamWriter sw, LocalsAllocator allocator) {
        // load operands onto the stack
        // left
        if (binaryOp.Left.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    ldsfld {TypeToILType(binaryOp.Left.Type)} {projectName}.Program::{binaryOp.Left.Name}");
        }
        else {
            sw.WriteLine($"    {LoadLocal(allocator.GetIndex(binaryOp.Left))}");
        }
        
        // right
        if (binaryOp.Right.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    ldsfld {TypeToILType(binaryOp.Right.Type)} {projectName}.Program::{binaryOp.Right.Name}");
        }
        else {
            sw.WriteLine($"    {LoadLocal(allocator.GetIndex(binaryOp.Right))}");
        }
        
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
        if (binaryOp.Dest.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    stsfld {TypeToILType(binaryOp.Dest.Type)} {projectName}.Program::{binaryOp.Dest.Name}");
        }
        else {
            sw.WriteLine($"    {StoreLocal(allocator.GetIndex(binaryOp.Dest))}");
        }
    }

    private void Generate(ILCall call, StreamWriter sw) {
        
    }

    private void Generate(ILGoto @goto, StreamWriter sw) {
        sw.WriteLine($"    br.s {@goto.Label}");
    }

    private void Generate(ILIfGoto ifGoto, StreamWriter sw, LocalsAllocator allocator) {
        // load condition
        if (ifGoto.Condition.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    ldsfld bool {projectName}.Program::{ifGoto.Condition.Name}");
        }
        else {
            sw.WriteLine($"    {LoadLocal(allocator.GetIndex(ifGoto.Condition))}");
        }
        // branch
        sw.WriteLine($"    brtrue.s {ifGoto.Label}");
    }

    private void Generate(ILLabel label, StreamWriter sw) {
        sw.WriteLine($"    {label.Name}:");
    }

    private void Generate(ILLoad load, StreamWriter sw, LocalsAllocator allocator) {
        if (load.Value is int i) {
            if (i >= -1 && i <= 8) {
                sw.WriteLine($"    ldc.i4.{i}");
            }
            else {
                sw.WriteLine($"    ldc.i4 {i}");
            }

            if (load.Dest.AddressType == ILAddressType.Global) {
                sw.WriteLine($"    stsfld int32 {projectName}.Program::{load.Dest.Name}");
            }else {
                sw.WriteLine($"    {StoreLocal(allocator.GetIndex(load.Dest))}");
            }
        }else if (load.Value is bool b) {
            sw.WriteLine($"    ldc.i4.{(b ? 1 : 0)}");
            if (load.Dest.AddressType == ILAddressType.Global) {
                sw.WriteLine($"    stsfld bool {projectName}.Program::{load.Dest.Name}");
            }
            else {
                sw.WriteLine($"    {StoreLocal(allocator.GetIndex(load.Dest))}");
            }
        }
        else {
            throw new NotSupportedException($"Type {load.Value.GetType()} not supported");
        }
    }
    
    private void Generate(ILLoadPtr loadPtr, StreamWriter sw, LocalsAllocator allocator) {
        sw.WriteLine($"    ldsfld string {projectName}.Program::{loadPtr.Src.Name}");
        sw.WriteLine($"    {StoreLocal(allocator.GetIndex(loadPtr.Dest))}");
    }
    
    private void Generate(ILMove move, StreamWriter sw, LocalsAllocator allocator) {
        if (move.Src.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    ldsfld {TypeToILType(move.Src.Type)} {projectName}.Program::{move.Src.Name}");
        }
        else {
            sw.WriteLine($"    {LoadLocal(allocator.GetIndex(move.Src))}");
        }

        if (move.Dest.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    stsfld {TypeToILType(move.Dest.Type)} {projectName}.Program::{move.Dest.Name}");
        }
        else {
            sw.WriteLine($"    {StoreLocal(allocator.GetIndex(move.Dest))}");
        }
    }
    
    private void Generate(ILRead read, StreamWriter sw) {
        
    }
    
    private void Generate(ILReturn @return, StreamWriter sw, LocalsAllocator allocator) {
        if (@return.Value is null) {
            sw.WriteLine("    ret");
            return;
        }
        
        if (@return.Value.AddressType == ILAddressType.Global) {
            sw.WriteLine($"    ldsfld {TypeToILType(@return.Value.Type)} {projectName}.Program::{@return.Value.Name}");
        }
        else {
            sw.WriteLine($"    {LoadLocal(allocator.GetIndex(@return.Value))}");
        }
        sw.WriteLine("    ret");
    }
    
    private void Generate(ILWrite write, StreamWriter sw, LocalsAllocator allocator) {
        switch (write.WriteType) {
            case Type.Integer:
                if (write.Src.AddressType == ILAddressType.Global) {
                    sw.WriteLine($"    ldsfld int32 {projectName}.Program::{write.Src.Name}");
                }
                else {
                    sw.WriteLine($"    {LoadLocal(allocator.GetIndex(write.Src))}");
                }

                sw.WriteLine("    call void [System.Console]System.Console::Write(int32)");
                sw.WriteLine("    nop");
                break;
            case Type.String:
                if (write.Src.AddressType == ILAddressType.Global) {
                    sw.WriteLine($"    ldsfld string {projectName}.Program::{write.Src.Name}");
                }else if (write.Src.AddressType == ILAddressType.Stack) {
                    sw.WriteLine($"    {LoadLocal(allocator.GetIndex(write.Src))}");
                }
                sw.WriteLine("    call void [System.Console]System.Console::Write(string)");
                sw.WriteLine("    nop");
                break;
            case Type.Boolean:
                if (write.Src.AddressType == ILAddressType.Global) {
                    sw.WriteLine($"    ldsfld bool {projectName}.Program::{write.Src.Name}");
                }else {
                    sw.WriteLine($"    {LoadLocal(allocator.GetIndex(write.Src))}");
                }
                sw.WriteLine("    call void [System.Console]System.Console::Write(bool)");
                sw.WriteLine("    nop");
                break;
        }
    }
    
    private void Generate(ILUnaryOp unaryOp, StreamWriter sw, LocalsAllocator allocator) {
        switch (unaryOp.Op) {
            case UnaryOperationType.Not:
                if (unaryOp.Dest.AddressType == ILAddressType.Global) {
                    sw.WriteLine($"    ldsfld bool {projectName}.Program::{unaryOp.Operand.Name}");
                }
                else {
                    sw.WriteLine($"    {LoadLocal(allocator.GetIndex(unaryOp.Operand))}");
                }
                sw.WriteLine("    ldc.i4.0");
                sw.WriteLine("    ceq");
                if (unaryOp.Dest.AddressType == ILAddressType.Global) {
                    sw.WriteLine($"    stsfld bool {projectName}.Program::{unaryOp.Dest.Name}");
                }
                else {
                    sw.WriteLine($"    {StoreLocal(allocator.GetIndex(unaryOp.Dest))}");
                }
                break;
            default:
                throw new NotSupportedException($"Unary op {unaryOp.Op} not supported");
        }
    }

    #endregion

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
                sb.Append(locals[i].Type switch {
                    Type.Integer => "int32",
                    Type.Boolean => "bool",
                    Type.String => "string",
                    _ => throw new NotSupportedException($"Type {locals[i].Type} not implemented")
                });

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
    
    private string TypeToILType(Type type) {
        return type switch {
            Type.Integer => "int32",
            Type.Boolean => "bool",
            Type.String => "string",
            Type.Void => "void",
            _ => throw new NotSupportedException($"Type {type} not implemented")
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