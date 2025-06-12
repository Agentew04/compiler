using System.Text;
using FortallCompiler.Ast;
using FortallCompiler.CodeGeneration.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class MipsGenerator
{
    ILProgram program;
    
    public void Generate(ILProgram program, Stream outputStream)
    {
        this.program = program;
        using StreamWriter sw = new(outputStream, leaveOpen: true);
        // write globals
        sw.WriteLine(".data");
        foreach (ILGlobalVariable global in program.Globals)
        {
            string type = global.Type switch
            {
                Type.Integer => ".word",
                Type.Boolean => ".byte",
                Type.String => ".asciiz",
                _ => throw new NotSupportedException($"Unsupported global variable type: {global.Type}")
            };
            string initialValue = global.Value switch
            {
                null => "",
                string str => $"\"{EscapeString(str)}\"",
                int i => i.ToString(),
                bool b => b ? "1" : "0",
                _ => throw new NotSupportedException($"Unsupported initial value type: {global.Value.GetType()}")
            };
            sw.WriteLine($"{global.Name}: {type} {initialValue}");
        }
        sw.WriteLine();
        
        sw.WriteLine(".text");
        sw.WriteLine($"j {program.MainLabel}");
        sw.WriteLine();
        
        // agora emite para cada funcao
        foreach (ILFunction function in program.Functions)
        {
            Generate(function, sw);
        }
    }

    private void Generate(ILFunction function, StreamWriter sw)
    {
        StringBuilder paramSb = new();
        int i = 0;
        foreach (string param in function.Parameters)
        {
            paramSb.Append($"\n#   {param}: a{i}");
            i++;
        }
        sw.WriteLine($"""
                      # FUNCAO: {function.Name}
                      # PARAMETROS:{paramSb}
                      """);

        ILLabel ilLabel = (ILLabel)function.Instructions[0];
        function.Instructions.RemoveAt(0);
        
        sw.WriteLine($"{ilLabel.Name}:");
        
        StackAllocator stackAllocator = new();
        stackAllocator.AllocateVariable("$ra");
        sw.WriteLine("\t# Mapa de offsets de variaveis:");
        // loopa 2 vezes, p primeiro adicionar tudo dps ler os offsets
        foreach (ILVar varDecl in function.Instructions.OfType<ILVar>())
        {
            stackAllocator.AllocateVariable(varDecl.Name);
        }
        foreach (ILVar varDecl in function.Instructions.OfType<ILVar>())
        {
            sw.WriteLine($"\t#   {varDecl.Name} -> $sp+{stackAllocator.GetVariableOffset(varDecl.Name)}");
        }
        
        // calcula os temporarios
        var temporaries = CollectTemporaries(function.Instructions);
        RegisterAllocator registerAllocator = new();
        // map temporaries to registers
        sw.WriteLine("\t# Mapa de temporarios para registradores:");
        foreach (ILAddress temporary in temporaries)
        {
            string reg = registerAllocator.AllocateRegister(temporary);
            sw.WriteLine($"\t#   {temporary} -> {reg}");
        } 

        // emit prologue
        sw.WriteLine("\t# PROLOGO");
        sw.WriteLine($"\taddi $sp, $sp, -{stackAllocator.GetStackSize()} # aloca espaco na pilha");
        sw.WriteLine($"\tsw ra, {stackAllocator.GetVariableOffset("$ra")}($sp) # salva endereco de retorno");

        sw.WriteLine("\t# CORPO");
        foreach (ILInstruction instruction in function.Instructions)
        {
            // pronto
            if(instruction is ILReturn ret)
            {
                Generate(ret, sw, stackAllocator, registerAllocator);
            }

            // Da pra simplificar isso pois apenas strings usam loadPtr, entao sempre sao globais
            if (instruction is ILLoadPtr loadPtr)
            {
                Generate(loadPtr, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILLoad load)
            {
                Generate(load, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILUnaryOp unaryOp)
            {
                Generate(unaryOp, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILMove move)
            {
                Generate(move, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILIfGoto ifGoto)
            {
                Generate(ifGoto, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILLabel label)
            {
                Generate(label, sw, stackAllocator, registerAllocator);
            }

            if (instruction is ILGoto goTo)
            {
                Generate(goTo, sw, stackAllocator, registerAllocator);
            }
        }
        sw.WriteLine();
    }

    private void Generate(ILReturn ret, StreamWriter sw, StackAllocator stackAllocator, 
        RegisterAllocator registerAllocator)
    {
        // restore $ra
        sw.WriteLine("\tlw ra, 0($fp) # restaura endereco de retorno");
        if (ret.Value is not null)
        {
            if (ret.Value.IsTemporary)
            {
                // se eh temporario, load no v0
                string reg = registerAllocator.GetRegister(ret.Value);
                sw.WriteLine($"\taddi $v0, {reg}, $zero # carrega valor de retorno");
            }
            else
            {
                // senao load da stack para v0
                int offset = stackAllocator.GetVariableOffset(ret.Value.Name);
                sw.WriteLine($"\tlw $v0, +{offset}($sp) # carrega valor de retorno");
            }
        }
        // libera a memoria do stack
        sw.WriteLine($"\taddi $sp, $sp, {stackAllocator.GetStackSize()} # libera espaco na pilha");
        // retorna
        sw.WriteLine("\tjr $ra # retorna");
    }
    
    private void Generate(ILLoadPtr loadptr, StreamWriter sw, StackAllocator stackAllocator, 
        RegisterAllocator registerAllocator)
    {
        if (!loadptr.Src.IsGlobal)
        {
            throw new Exception("erro!. LoadPtr deve ser usado apenas com variaveis globais.");
        }

        if (loadptr.Src.IsGlobal)
        {
            sw.WriteLine($"\tla $at, {loadptr.Src.Name} # carrega endereco de {loadptr.Src.Name} em $at");
        }else if (!loadptr.Src.IsTemporary)
        {
            // ta na stack
            sw.WriteLine($"\tadd $at, $sp, {stackAllocator.GetVariableOffset(loadptr.Src.Name)} # carrega endereco de {loadptr.Src.Name} em $at");
        }
        else
        {
            throw new Exception("Erro! Impossivel carregar ponteiro de um registrador.");
        }

        if (loadptr.Dest.IsTemporary)
        {
            sw.WriteLine($"\taddi {registerAllocator.GetRegister(loadptr.Dest)}, $at, 0 # carrega endereco de {loadptr.Src.Name} em {loadptr.Dest}");
        }else if (loadptr.Dest.IsGlobal)
        {
            // e depois copia o valor do ponteiro para a variavel global
            sw.WriteLine($"\tsw $at, {loadptr.Dest.Name} # armazena endereco de {loadptr.Src.Name} em {loadptr.Dest.Name}");
        }
        else
        {
            // eh stack
            sw.WriteLine($"\tsw $at, {stackAllocator.GetVariableOffset(loadptr.Dest.Name)}($sp) # armazena endereco de {loadptr.Src.Name} em {loadptr.Dest}");
        }
    }

    private void Generate(ILLoad load, StreamWriter sw, StackAllocator stackAllocator, 
        RegisterAllocator registerAllocator)
    {
        // aqui sabemos que nao eh string.
        int value;
        if (load.Value is bool)
        {
            value = (bool)load.Value ? 1 : 0;
        }
        else
        {
            value = (int)load.Value;
        }

        if (load.Dest.IsTemporary)
        {
            // em registrador
            sw.WriteLine($"\taddi {registerAllocator.GetRegister(load.Dest)}, $zero, {value} # guarda valor {value} em {load.Dest}");
        }
        else if(!load.Dest.IsGlobal)
        {
            // no stack
            sw.WriteLine($"\taddi $at, $zero, {value}");
            sw.WriteLine($"\tsw $at, {stackAllocator.GetVariableOffset(load.Dest.Name)}($sp) # guarda valor {value} em {load.Dest}");
        }
        else
        {
            // global
            sw.WriteLine($"\taddi $at, $zero, {value}");
            sw.WriteLine($"\tsw $at, {load.Dest.Name} # guarda valor {value} em {load.Dest.Name}");
        }
    }

    private void Generate(ILUnaryOp unaryOp, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        if (unaryOp.Operand.IsTemporary)
        {
            sw.WriteLine($"\tadd $at, {registerAllocator.GetRegister(unaryOp.Operand)}, $zero");
        }else if (unaryOp.Operand.IsGlobal)
        {
            sw.WriteLine($"\tlw $at, {unaryOp.Operand.Name} # carrega valor de {unaryOp.Operand.Name} em $at");
        }
        else
        {
            // stack
            sw.WriteLine($"\tlw $at, {stackAllocator.GetVariableOffset(unaryOp.Operand.Name)}($sp) # carrega valor de {unaryOp.Operand.Name} em $at");
        }
        
        // operando esta no $at
        switch (unaryOp.Op)
        {
            case UnaryOperationType.Not:
                sw.WriteLine($"\txori $at, $at, 1 # nega valor de {unaryOp.Operand} e armazena em {unaryOp.Dest}");
                break;
            default:
                throw new NotSupportedException($"Unsupported unary operation: {unaryOp.Op}");
        }
        
        // resultado esta no $at
        if (unaryOp.Dest.IsTemporary)
        {
            sw.WriteLine($"\tadd {registerAllocator.GetRegister(unaryOp.Dest)}, $at, $zero # armazena resultado de {unaryOp.Operand} em {unaryOp.Dest}");
        }
        else if (unaryOp.Dest.IsGlobal)
        {
            sw.WriteLine($"\tsw $at, {unaryOp.Dest.Name} # armazena resultado de {unaryOp.Operand} em {unaryOp.Dest.Name}");
        }
        else
        {
            // stack
            sw.WriteLine($"\tsw $at, {stackAllocator.GetVariableOffset(unaryOp.Dest.Name)}($sp) # armazena resultado de {unaryOp.Operand} em {unaryOp.Dest}");
        }
    }

    private void Generate(ILMove move, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        if (move.Src.IsTemporary && move.Dest.IsTemporary)
        {
            sw.WriteLine($"\tadd {registerAllocator.GetRegister(move.Dest)}, {registerAllocator.GetRegister(move.Src)}, $zero # move {move.Src} para {move.Dest}");
            return;
        }

        string dest = "$at";
        if (move.Src.IsTemporary)
        {
            dest = registerAllocator.GetRegister(move.Src);
        }
        else if (move.Src.IsGlobal)
        {
            sw.WriteLine($"\tlw $at, {move.Src.Name} # carrega valor de {move.Src.Name} em $at");
        }
        else
        {
            // stack
            sw.WriteLine($"\tlw $at, {stackAllocator.GetVariableOffset(move.Src.Name)}($sp) # carrega valor de {move.Src.Name} em $at");
        }
        
        // valor esta em dest
        if (move.Dest.IsTemporary)
        {
            sw.WriteLine($"\tadd {registerAllocator.GetRegister(move.Dest)}, {dest}, $zero # move valor de {move.Src} para {move.Dest}");
        }
        else if (move.Dest.IsGlobal)
        {
            sw.WriteLine($"\tsw {dest}, {move.Dest.Name} # move valor de {move.Src} para {move.Dest.Name}");
        }
        else
        {
            // stack
            sw.WriteLine($"\tsw {dest}, {stackAllocator.GetVariableOffset(move.Dest.Name)}($sp) # move valor de {move.Src} para {move.Dest}");
        }
    }
    
    private void Generate(ILIfGoto ifGoto, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        // bnez
        // simplificacao da implementacao
        // sabemos de ctz q a condicao eh temporaria, pois sempre eh negada
        if (!ifGoto.Condition.IsTemporary)
        {
            throw new NotSupportedException("Condition must be a temporary address. This is a TODO");
        }
        string reg = registerAllocator.GetRegister(ifGoto.Condition);
        sw.WriteLine($"\tbnez {reg}, {ifGoto.Label} # se {ifGoto.Condition} for diferente de zero, pula para {ifGoto.Label}");
    }
    
    private void Generate(ILLabel label, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        sw.WriteLine($"\t{label.Name}: # label {label.Name}");
    }
    
    private void Generate(ILGoto goTo, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        // j
        sw.WriteLine($"\tj {goTo.Label} # pula para {goTo.Label}");
    }
    
    private class StackAllocator
    {
        private List<string> variables = [];
        
        public void AllocateVariable(string name)
        {
            variables.Add(name);
        }
        
        public void DisposeVariable(string name)
        {
            if(variables[^1] != name)
            {
                throw new InvalidOperationException($"Cannot dispose variable {name} because it is not the last allocated variable.");
            }
            variables.RemoveAt(variables.Count - 1);
        }

        public int GetVariableOffset(string name)
        {
            int index = variables.IndexOf(name);
            int offset = (variables.Count - (index + 1)) * 4;
            return offset;
        }
        
        public int GetStackSize()
        {
            return variables.Count * 4; // each variable takes 4 bytes
        }
    }

    private class RegisterAllocator
    {
        private Dictionary<ILAddress, string> tempToRegister = [];
        private Dictionary<string, ILAddress> registerToTemp = [];
        private Queue<string> freeRegisters;

        public RegisterAllocator()
        {
            freeRegisters = new Queue<string>(new[] {
                "$t0", "$t1", "$t2", "$t3", "$t4", "$t5", "$t6", "$t7", "$t8", "$t9"
            });
        }

        public string AllocateRegister(ILAddress temporary)
        {
            if (tempToRegister.ContainsKey(temporary))
            {
                return tempToRegister[temporary];
            }
            if (freeRegisters.Count == 0)
            {
                throw new InvalidOperationException("No free registers available.");
            }
            
            string reg = freeRegisters.Dequeue();
            tempToRegister[temporary] = reg;
            registerToTemp[reg] = temporary;
            return reg;
        }

        public string GetRegister(ILAddress register)
        {
            if (tempToRegister.TryGetValue(register, out string reg))
            {
                return reg;
            }
            throw new KeyNotFoundException($"Temporary address {register} is not allocated to any register.");
        }

        public void DisposeRegister(string register)
        {
            if (!registerToTemp.ContainsKey(register))
            {
                throw new KeyNotFoundException($"Register {register} is not allocated to any temporary address.");
            }
            
            ILAddress temp = registerToTemp[register];
            tempToRegister.Remove(temp);
            registerToTemp.Remove(register);
            freeRegisters.Enqueue(register);
        }
    }
    
    // private void Generate(ILInstruction instruction, StreamWriter sw)
    // {
    //     switch (instruction)
    //     {
    //         case ILLabel label:
    //             sw.WriteLine($"\t{label.Name}:");
    //             break;
    //         case ILCall call:
    //             // TODO: save temporary registers if needed
    //             string funcLabel = program.Functions.First(x => x.Name == call.FunctionName).Label;
    //             sw.WriteLine($"\tjal {funcLabel}");
    //             break;
    //         default:
    //             sw.WriteLine("INSTRUCAO NAO CONHECIDA");
    //             break;
    //     }
    // }
    
    private string EscapeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder result = new(value.Length + value.Length / 2);

        foreach (char c in value)
        {
            switch (c)
            {
                case '\0': result.Append(@"\0"); break;
                case '\a': result.Append(@"\a"); break;
                case '\b': result.Append(@"\b"); break;
                case '\t': result.Append(@"\t"); break;
                case '\n': result.Append(@"\n"); break;
                case '\v': result.Append(@"\v"); break;
                case '\f': result.Append(@"\f"); break;
                case '\r': result.Append(@"\r"); break;
                case '\\': result.Append(@"\\"); break;
                case '"': result.Append(@"\"""); break;
                default:
                    if (c >= ' ')
                        result.Append(c);
                    else // the character is in the 0..31 range
                        result.Append($@"\x{(int)c:X2}");
                    break;
            }
        }

        return result.ToString();
    }
    
    private List<ILAddress> CollectTemporaries(List<ILInstruction> instructions)
    {
        HashSet<ILAddress> temporaries = [];
        foreach (ILInstruction instruction in instructions)
        {
            switch (instruction)
            {
                case ILBinaryOp ilBinaryOp:
                    if (ilBinaryOp.Left.IsTemporary)
                    {
                        temporaries.Add(ilBinaryOp.Left);
                    }
                    if (ilBinaryOp.Right.IsTemporary)
                    {
                        temporaries.Add(ilBinaryOp.Right);
                    }
                    if (ilBinaryOp.Dest.IsTemporary)
                    {
                        temporaries.Add(ilBinaryOp.Dest);
                    }
                    break;
                case ILCall ilCall:
                    foreach (ILAddress arg in ilCall.Arguments)
                    {
                        if (arg.IsTemporary)
                        {
                            temporaries.Add(arg);
                        }
                    }
                    if(ilCall.Dest is not null && ilCall.Dest.IsTemporary)
                    {
                        temporaries.Add(ilCall.Dest);
                    }
                    break;
                case ILGoto ilGoto:
                    break;
                case ILIfGoto ilIfGoto:
                    if (ilIfGoto.Condition.IsTemporary)
                    {
                        temporaries.Add(ilIfGoto.Condition);
                    }
                    break;
                case ILLabel ilLabel:
                    break;
                case ILLoad ilLoad:
                    if (ilLoad.Dest.IsTemporary)
                    {
                        temporaries.Add(ilLoad.Dest);
                    }
                    break;
                case ILMove ilMove:
                    if (ilMove.Dest.IsTemporary)
                    {
                        temporaries.Add(ilMove.Dest);
                    }
                    if (ilMove.Src.IsTemporary)
                    {
                        temporaries.Add(ilMove.Src);
                    }
                    break;
                case ILRead ilRead:
                    if (ilRead.Dest.IsTemporary)
                    {
                        temporaries.Add(ilRead.Dest);
                    }
                    break;
                case ILReturn ilReturn:
                    if (ilReturn.Value is not null && ilReturn.Value.IsTemporary)
                    {
                        temporaries.Add(ilReturn.Value);
                    }
                    break;
                case ILUnaryOp ilUnaryOp:
                    if (ilUnaryOp.Dest.IsTemporary)
                    {
                        temporaries.Add(ilUnaryOp.Dest);
                    }
                    if (ilUnaryOp.Operand.IsTemporary)
                    {
                        temporaries.Add(ilUnaryOp.Operand);
                    }
                    break;
                case ILVar ilVar:
                    break;
                case ILWrite ilWrite:
                    if (ilWrite.Src.IsTemporary)
                    {
                        temporaries.Add(ilWrite.Src);
                    }
                    break;
                case ILLoadPtr ilLoadPtr:
                    if (ilLoadPtr.Dest.IsTemporary)
                    {
                        temporaries.Add(ilLoadPtr.Dest);
                    }
                    if (ilLoadPtr.Src.IsTemporary)
                    {
                        temporaries.Add(ilLoadPtr.Src);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(instruction));
            }
        }
        return temporaries.Order().ToList();
    }
}