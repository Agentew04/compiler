using System.Text;
using FortallCompiler.Ast;
using FortallCompiler.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.Steps;

public class MipsGenerator
{
    ILProgram program;
    
    public void Generate(ILProgram program, Stream outputStream)
    {
        this.program = program;
        using StreamWriter sw = new(outputStream, leaveOpen: true);
        // write globals
        sw.WriteLine(".globl __start");
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
            if (global.Type == Type.String && global.Value is null)
            {
                // tamanho maximo para strings
                type = ".space 256";
            }
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
        sw.WriteLine("__program_end:");
        sw.WriteLine();
    }

    private void Generate(ILFunction function, StreamWriter sw)
    {
        StringBuilder paramSb = new();
        int i = 0;
        foreach (string param in function.Parameters)
        {
            paramSb.Append($"\n//   {param}: a{i}");
            i++;
        }
        sw.WriteLine($"""
                      // FUNCAO: {function.Name}
                      // PARAMETROS:{paramSb}
                      """);

        ILLabel ilLabel = (ILLabel)function.Instructions[0];
        function.Instructions.RemoveAt(0);

        if (function.Name == "main") {
            sw.WriteLine("__start:");
        }
        
        sw.WriteLine($"{ilLabel.Name}:");
        
        StackAllocator stackAllocator = new();
        if (function.Name != "main") {
            stackAllocator.AllocateVariable("$ra");
        }
        sw.WriteLine("\t// Mapa de offsets de variaveis:");
        // aloca todos os parametros
        foreach (string param in function.Parameters)
        {
            stackAllocator.AllocateVariable(param);
        }
        // alloca todas variaveis
        foreach (ILVar varDecl in function.Instructions.OfType<ILVar>())
        {
            stackAllocator.AllocateVariable(varDecl.Name);
        }
        foreach (string param in function.Parameters)
        {
            sw.WriteLine($"\t//   {param} -> $sp+{stackAllocator.GetVariableOffset(param)}");
        }
        foreach (ILVar varDecl in function.Instructions.OfType<ILVar>())
        {
            sw.WriteLine($"\t//   {varDecl.Name} -> $sp+{stackAllocator.GetVariableOffset(varDecl.Name)}");
        }
        
        // calcula os temporarios
        var temporaries = CollectTemporaries(function.Instructions);
        RegisterAllocator registerAllocator = new();
        // map temporaries to registers
        sw.WriteLine("\t// Mapa de temporarios para registradores:");
        foreach (ILAddress temporary in temporaries)
        {
            string reg = registerAllocator.AllocateRegister(temporary);
            sw.WriteLine($"\t//   {temporary} -> {reg}");
        } 

        // emit prologue
        sw.WriteLine("\t// PROLOGO");
        sw.WriteLine($"\taddi $sp, $sp, -{stackAllocator.GetStackSize()} // aloca espaco na pilha");
        if (function.Name != "main") {
            sw.WriteLine($"\tsw $ra, {stackAllocator.GetVariableOffset("$ra")}($sp) // salva endereco de retorno");
        }

        // salva parametros no stack
        for (i = 0; i < function.Parameters.Count; i++) {
            sw.WriteLine($"\tsw $a{i}, {stackAllocator.GetVariableOffset(function.Parameters[i])}($sp) // salva argumento {function.Parameters[i]} na pilha");
        }

        sw.WriteLine("\t// CORPO");
        foreach (ILInstruction instruction in function.Instructions)
        {
            switch (instruction)
            {
                case ILReturn ret:
                    Generate(ret, sw, stackAllocator, registerAllocator);
                    break;
                case ILLoadPtr loadPtr:
                    Generate(loadPtr, sw, stackAllocator, registerAllocator);
                    break;
                case ILLoad load:
                    Generate(load, sw, stackAllocator, registerAllocator);
                    break;
                case ILUnaryOp unaryOp:
                    Generate(unaryOp, sw, stackAllocator, registerAllocator);
                    break;
                case ILMove move:
                    Generate(move, sw, stackAllocator, registerAllocator);
                    break;
                case ILIfGoto ifGoto:
                    Generate(ifGoto, sw, stackAllocator, registerAllocator);
                    break;
                case ILLabel label:
                    Generate(label, sw, stackAllocator, registerAllocator);
                    break;
                case ILGoto goTo:
                    Generate(goTo, sw, stackAllocator, registerAllocator);
                    break;
                case ILCall call:
                    Generate(call, sw, stackAllocator, registerAllocator);
                    break;
                case ILBinaryOp binaryOp:
                    Generate(binaryOp, sw, stackAllocator, registerAllocator);
                    break;
                case ILWrite write:
                    Generate(write, sw, stackAllocator, registerAllocator);
                    break;
                case ILRead read:
                    Generate(read, sw, stackAllocator, registerAllocator);
                    break;
            }
        }
        if (function.Name == "main") {
            sw.WriteLine("\tj __program_end");
        }
        sw.WriteLine();
    }

    private void Generate(ILReturn ret, StreamWriter sw, StackAllocator stackAllocator, 
        RegisterAllocator registerAllocator)
    {
        // restore $ra
        bool isMain = stackAllocator.GetVariableOffset("$ra") == -1; 
        if (!isMain) {
            sw.WriteLine($"\tlw $ra, {stackAllocator.GetVariableOffset("$ra")}($sp) // restaura endereco de retorno");
        }
        if (ret.Value is not null)
        {
            if (ret.Value.AddressType == ILAddressType.Temporary)
            {
                // se eh temporario, load no v0
                string reg = registerAllocator.GetRegister(ret.Value);
                sw.WriteLine($"\tadd $v0, {reg}, $zero // carrega valor de retorno");
            }else if (ret.Value.AddressType == ILAddressType.Global) {
                sw.WriteLine($"\tlw $v0, {ret.Value.Label}");
            }
            else
            {
                // senao load da stack para v0
                int offset = stackAllocator.GetVariableOffset(ret.Value.Name);
                sw.WriteLine($"\tlw $v0, +{offset}($sp) // carrega valor de retorno");
            }
        }
        else {
            sw.WriteLine("\tadd $v0, $zero, $zero // funcao nao tem retorno, seta como 0");
        }
        // libera a memoria do stack
        sw.WriteLine($"\taddi $sp, $sp, {stackAllocator.GetStackSize()} // libera espaco na pilha");
        // retorna
        if (isMain) {
            // termina a execucao
            int syscall = ret.Value is not null ? 17 : 10;
            if (syscall == 17) {
                sw.WriteLine("\tadd $a0, $v0, $zero // passa codigo de saida do programa");
            }
            sw.WriteLine($"\tli $v0, {syscall}");
            sw.WriteLine("\tsyscall // termina a execucao");
        }
        else {
            // volta pra funcao anterior
            sw.WriteLine("\tjr $ra // retorna");
        }
    }
    
    private void Generate(ILLoadPtr loadptr, StreamWriter sw, StackAllocator stackAllocator, 
        RegisterAllocator registerAllocator) {
        string temp = registerAllocator.AllocateRegister(new ILAddress("temporary", ILAddressType.Temporary));
        switch (loadptr.Src.AddressType) {
            case ILAddressType.Temporary:
                throw new Exception("erro!. load ptr n pode ser usado com registradores.");
            case ILAddressType.Global:
                sw.WriteLine($"\tla {temp}, {loadptr.Src.Label} // carrega endereco de {loadptr.Src.Name} em {temp}");
                break;
            case ILAddressType.Stack:
                // ta na stack
                sw.WriteLine($"\tadd {temp}, $sp, {stackAllocator.GetVariableOffset(loadptr.Src.Name)} // carrega endereco de {loadptr.Src.Name} em {temp}");
                break;
        }

        switch (loadptr.Dest.AddressType) {
            case ILAddressType.Temporary:
                sw.WriteLine($"\taddi {registerAllocator.GetRegister(loadptr.Dest)}, {temp}, 0 // armazena endereco de {loadptr.Src.Name} em {loadptr.Dest}");
                break;
            case ILAddressType.Global:
                // e depois copia o valor do ponteiro para a variavel global
                sw.WriteLine($"\tsw {temp}, {loadptr.Dest.Label} // armazena endereco de {loadptr.Src.Name} em {loadptr.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // eh stack
                sw.WriteLine($"\tsw {temp}, {stackAllocator.GetVariableOffset(loadptr.Dest.Name)}($sp) // armazena endereco de {loadptr.Src.Name} em {loadptr.Dest}");
                break;
        }
        registerAllocator.DisposeRegister(temp);
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

        string temp = registerAllocator.AllocateRegister(new ILAddress("temporary", ILAddressType.Temporary));
        
        switch (load.Dest.AddressType) {
            case ILAddressType.Temporary:
                // em registrador
                sw.WriteLine($"\taddi {registerAllocator.GetRegister(load.Dest)}, $zero, {value} // guarda valor {value} em {load.Dest}");
                break;
            case ILAddressType.Stack:
                // no stack
                sw.WriteLine($"\taddi {temp}, $zero, {value}");
                sw.WriteLine($"\tsw {temp}, {stackAllocator.GetVariableOffset(load.Dest.Name)}($sp) // guarda valor {value} em {load.Dest}");
                break;
            case ILAddressType.Global:
                // global
                sw.WriteLine($"\taddi {temp}, $zero, {value}");
                sw.WriteLine($"\tsw {temp}, {load.Dest.Label} // guarda valor {value} em {load.Dest.Name}");
                break;
        }
        registerAllocator.DisposeRegister(temp);
    }

    private void Generate(ILUnaryOp unaryOp, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        string temp = registerAllocator.AllocateRegister(new ILAddress("temporary", ILAddressType.Temporary));
        switch (unaryOp.Operand.AddressType) {
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd {temp}, {registerAllocator.GetRegister(unaryOp.Operand)}, $zero");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tlw {temp}, {unaryOp.Operand.Label} // carrega valor de {unaryOp.Operand.Name} em {temp}");
                break;
            default:
                // stack
                sw.WriteLine($"\tlw {temp}, {stackAllocator.GetVariableOffset(unaryOp.Operand.Name)}($sp) // carrega valor de {unaryOp.Operand.Name} em {temp}");
                break;
        }
        
        // operando esta no temp
        switch (unaryOp.Op)
        {
            case UnaryOperationType.Not:
                sw.WriteLine($"\txori {temp}, {temp}, 1 // nega valor de {unaryOp.Operand} e armazena em {unaryOp.Dest}");
                break;
            default:
                throw new NotSupportedException($"Unsupported unary operation: {unaryOp.Op}");
        }

        switch (unaryOp.Dest.AddressType) {
            // resultado esta no temp
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd {registerAllocator.GetRegister(unaryOp.Dest)}, {temp}, $zero // armazena resultado de {unaryOp.Operand} em {unaryOp.Dest}");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tsw {temp}, {unaryOp.Dest.Label} // armazena resultado de {unaryOp.Operand} em {unaryOp.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tsw {temp}, {stackAllocator.GetVariableOffset(unaryOp.Dest.Name)}($sp) // armazena resultado de {unaryOp.Operand} em {unaryOp.Dest}");
                break;
        }
        registerAllocator.DisposeRegister(temp);
    }

    private void Generate(ILMove move, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        if (move.Src.AddressType == ILAddressType.Temporary && move.Dest.AddressType == ILAddressType.Temporary)
        {
            sw.WriteLine($"\tadd {registerAllocator.GetRegister(move.Dest)}, {registerAllocator.GetRegister(move.Src)}, $zero // move {move.Src} para {move.Dest}");
            return;
        }

        string temp = registerAllocator.AllocateRegister(new ILAddress("temporary", ILAddressType.Temporary));
        string dest = temp;
        switch (move.Src.AddressType) {
            case ILAddressType.Temporary:
                dest = registerAllocator.GetRegister(move.Src);
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tlw {temp}, {move.Src.Label} // carrega valor de {move.Src.Name} em {temp}");
                break;
            default:
                // stack
                sw.WriteLine($"\tlw {temp}, {stackAllocator.GetVariableOffset(move.Src.Name)}($sp) // carrega valor de {move.Src.Name} em {temp}");
                break;
        }

        switch (move.Dest.AddressType) {
            // valor esta em dest
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd {registerAllocator.GetRegister(move.Dest)}, {dest}, $zero // move valor de {move.Src} para {move.Dest}");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tsw {dest}, {move.Dest.Label} // move valor de {move.Src} para {move.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tsw {dest}, {stackAllocator.GetVariableOffset(move.Dest.Name)}($sp) // move valor de {move.Src} para {move.Dest}");
                break;
        }
        registerAllocator.DisposeRegister(temp);
    }
    
    private void Generate(ILIfGoto ifGoto, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        // bnez
        // simplificacao da implementacao
        // sabemos de ctz q a condicao eh temporaria, pois sempre eh negada
        if (ifGoto.Condition.AddressType != ILAddressType.Temporary)
        {
            throw new NotSupportedException("Condition must be a temporary address. This is a TODO");
        }
        string reg = registerAllocator.GetRegister(ifGoto.Condition);
        sw.WriteLine($"\tbnez {reg}, {ifGoto.Label} // se {ifGoto.Condition} for diferente de zero, pula para {ifGoto.Label}");
    }
    
    private void Generate(ILLabel label, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        sw.WriteLine($"\t{label.Name}: // label {label.Name}");
    }
    
    private void Generate(ILGoto goTo, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        // j
        sw.WriteLine($"\tj {goTo.Label} // pula para {goTo.Label}");
    }

    private void Generate(ILCall call, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        if(call.Arguments.Count > 4)
        {
            throw new NotSupportedException("Cannot call functions with more than 4 arguments in MIPS.");
        }
        // carrega os argumentos nos registradores de argumento
        for (int i = 0; i < call.Arguments.Count; i++) {
            ILAddress arg = call.Arguments[i];
            switch (arg.AddressType) {
                case ILAddressType.Temporary:
                    // se eh temporario, carrega no registrador de argumento
                    sw.WriteLine($"\tadd $a{i}, {registerAllocator.GetRegister(arg)}, $zero // carrega argumento {i} ({arg})");
                    break;
                case ILAddressType.Global:
                    // se eh global, carrega no registrador de argumento
                    sw.WriteLine($"\tlw $a{i}, {arg.Label} // carrega argumento {i} ({arg})");
                    break;
                case ILAddressType.Stack:
                    // se eh stack, carrega no registrador de argumento
                    sw.WriteLine($"\tlw $a{i}, {stackAllocator.GetVariableOffset(arg.Name)}($sp) // carrega argumento {i} ({arg})");
                    break;
            }
        }
        
        // argumentos preparados, agora tem que salvar o valor dos temporarios utilizados
        List<string> used = registerAllocator.GetUsedRegisters();
        // allocate memory for each used register
        sw.WriteLine("\t// SALVA TEMPORARIOS");
        sw.WriteLine($"\taddi $sp, $sp, -{used.Count * 4} // aloca espaco na pilha para temporarios");
        for (int i = 0; i < used.Count; i++)
        {
            string reg = used[i];
            // save each register to the stack
            sw.WriteLine($"\tsw {reg}, {i * 4}($sp) // salva {reg} na pilha");
        }
        // chama a funcao
        string funcLabel = null!;
        foreach (ILFunction func in program.Functions)
        {
            if(func.Name != call.FunctionName) continue;
            funcLabel = func.Label;
        }
        sw.WriteLine($"\tjal {funcLabel} // chama funcao {call.FunctionName}");
        // agora restaura os temporarios
        sw.WriteLine("\t// RESTAURA TEMPORARIOS");
        for (int i = used.Count - 1; i >= 0; i--)
        {
            string reg = used[i];
            // restore each register from the stack
            sw.WriteLine($"\tlw {reg}, {i * 4}($sp) // restaura {reg} da pilha");
            // n descarta pq ainda vai usar p frente
        }
        // desaloca o espaco da pilha dos temporarios
        sw.WriteLine($"\taddi $sp, $sp, {used.Count * 4} // libera espaco na pilha dos temporarios");
        
        // se a funcao tem retorno, pega do v0
        if (call.Dest is null) return;
        switch (call.Dest.AddressType) {
            case ILAddressType.Temporary:
                // se eh temporario, carrega no registrador de destino
                sw.WriteLine($"\tadd {registerAllocator.GetRegister(call.Dest)}, $v0, $zero // carrega retorno da funcao em {call.Dest}");
                break;
            case ILAddressType.Global:
                // se eh global, carrega no registrador de destino
                sw.WriteLine($"\tsw $v0, {call.Dest.Label} // armazena retorno da funcao em {call.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // se eh stack, carrega no registrador de destino
                sw.WriteLine($"\tsw $v0, {stackAllocator.GetVariableOffset(call.Dest.Name)}($sp) // armazena retorno da funcao em {call.Dest}");
                break;
        }
    }

    private void Generate(ILBinaryOp binaryOp, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator) {
        string temp = registerAllocator.AllocateRegister(new ILAddress("temporary", ILAddressType.Temporary));
        bool leftUsesTemp = true;
        string leftReg = temp;
        string rightReg = "";

        switch (binaryOp.Left.AddressType) {
            // carrega esquerda
            case ILAddressType.Temporary:
                leftUsesTemp = false;
                leftReg = registerAllocator.GetRegister(binaryOp.Left);
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tlw {temp}, {binaryOp.Left.Label} // carrega valor de {binaryOp.Left.Name} em {temp}");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tlw {temp}, {stackAllocator.GetVariableOffset(binaryOp.Left.Name)}($sp) // carrega valor de {binaryOp.Left.Name} em {temp}");
                break;
        }

        bool rightAllocated = false;
        switch (binaryOp.Right.AddressType) {
            case ILAddressType.Temporary:
                rightReg = registerAllocator.GetRegister(binaryOp.Right);
                break;
            case ILAddressType.Global:
                rightReg = leftUsesTemp ? registerAllocator.AllocateRegister(binaryOp.Right) : temp;
                rightAllocated = rightReg != temp;
                sw.WriteLine($"\tlw {rightReg}, {binaryOp.Right.Label} // carrega valor de {binaryOp.Right.Name} em {rightReg}");
                break;
            case ILAddressType.Stack:
                // no stack
                rightReg = leftUsesTemp ? registerAllocator.AllocateRegister(binaryOp.Right) : temp;
                rightAllocated = rightReg != temp;
                sw.WriteLine($"\tlw {rightReg}, {stackAllocator.GetVariableOffset(binaryOp.Right.Name)}($sp) // carrega valor de {binaryOp.Right.Name} em {rightReg}");
                break;
        }
        
        // ok, left e right estao carregados
        switch (binaryOp.Op)
        {
            case BinaryOperationType.Addition:
                sw.WriteLine($"\tadd {temp}, {leftReg}, {rightReg} // soma {binaryOp.Left} e {binaryOp.Right}");
                break;
            case BinaryOperationType.Subtraction:
                sw.WriteLine($"\tsub {temp}, {leftReg}, {rightReg} // subtrai {binaryOp.Right} de {binaryOp.Left}");
                break;
            case BinaryOperationType.Multiplication:
                // MUL coloca resultado no HI e LO e copia do LO para $at
                sw.WriteLine($"\tmul {temp}, {leftReg}, {rightReg} // multiplica {binaryOp.Left} e {binaryOp.Right}");
                break;
            case BinaryOperationType.Division:
                sw.WriteLine($"\tdiv {temp}, {leftReg}, {rightReg} // divide {binaryOp.Left} por {binaryOp.Right}");
                break;
            case BinaryOperationType.Equals:
                sw.WriteLine($"\tseq {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} == {binaryOp.Right}");
                break;
            case BinaryOperationType.NotEquals:
                sw.WriteLine($"\tsne {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} != {binaryOp.Right}");
                break;
            case BinaryOperationType.LessThan:
                sw.WriteLine($"\tslt {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} < {binaryOp.Right}");
                break;
            case BinaryOperationType.LessEqualThan:
                sw.WriteLine($"\tsle {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} <= {binaryOp.Right}");
                break;
            case BinaryOperationType.GreaterThan:
                sw.WriteLine($"\tsgt {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} > {binaryOp.Right}");
                break;
            case BinaryOperationType.GreaterEqualThan:
                sw.WriteLine($"\tsge {temp}, {leftReg}, {rightReg} // compara {binaryOp.Left} >= {binaryOp.Right}");
                break;
            case BinaryOperationType.And:
                sw.WriteLine($"\tand {temp}, {leftReg}, {rightReg} // realiza AND bit a bit entre {binaryOp.Left} e {binaryOp.Right}");
                break;
            case BinaryOperationType.Or:
                sw.WriteLine($"\tor {temp}, {leftReg}, {rightReg} // realiza OR bit a bit entre {binaryOp.Left} e {binaryOp.Right}");
                break;
            default:
                throw new NotSupportedException($"Unsupported binary operation: {binaryOp.Op}");
        }
        
        // resultado esta no temp

        switch (binaryOp.Dest.AddressType) {
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd {registerAllocator.GetRegister(binaryOp.Dest)}, {temp}, $zero // armazena resultado de {binaryOp.Left} e {binaryOp.Right} em {binaryOp.Dest}");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tsw {temp}, {binaryOp.Dest.Label} // armazena resultado de {binaryOp.Left} e {binaryOp.Right} em {binaryOp.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tsw {temp}, {stackAllocator.GetVariableOffset(binaryOp.Dest.Name)}($sp) // armazena resultado de {binaryOp.Left} e {binaryOp.Right} em {binaryOp.Dest}");
                break;
        }

        if (rightAllocated)
        {
            // se alocou um registrador para o right, libera ele
            registerAllocator.DisposeRegister(rightReg);
        }
        registerAllocator.DisposeRegister(temp);
    }

    private void Generate(ILWrite write, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
        int syscall = write.WriteType switch
        {
            Type.Integer => 1,
            Type.String => 4,
            Type.Boolean => 45, // implementado como syscall 45 extra. nao existente no mars
            Type.Void => -1,
            _ => throw new NotSupportedException($"Unsupported write type: {write.WriteType}")
        };
        if (syscall == -1)
        {
            sw.WriteLine("\tnop // syscall nao reconhecida!");
            return;
        }

        switch (write.Src.AddressType) {
            // move argumento para $a0
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd $a0, {registerAllocator.GetRegister(write.Src)}, $zero // move valor de {write.Src} para $a0 para syscall");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tla $a0, {write.Src.Label} // carrega endereco base de {write.Src.Name} em $a0");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tlw $a0, {stackAllocator.GetVariableOffset(write.Src.Name)}($sp) // carrega valor de {write.Src.Name} em $a0");
                break;
        }
        // coloca valor da syscall em v0
        sw.WriteLine($"\taddi $v0, $zero, {syscall}");
        // faz syscall
        sw.WriteLine($"\tsyscall // faz syscall {syscall} para escrever {write.WriteType}");
    }

    private void Generate(ILRead read, StreamWriter sw, StackAllocator stackAllocator,
        RegisterAllocator registerAllocator)
    {
     // read int: 5
     // read str: 8
     // read bool: 46
        int syscall = read.ReadType switch
        {
            Type.Integer => 5,
            Type.String => 8,
            Type.Boolean => 46,
            Type.Void => -1,
            _ => throw new NotSupportedException($"Unsupported read type: {read.ReadType}")
        };
        if (syscall == -1)
        {
            sw.WriteLine("\tnop // syscall nao reconhecida!");
            return;
        }
        // coloca syscall em v0
        sw.WriteLine($"\taddi $v0, $zero, {syscall} // syscall {syscall} para ler {read.ReadType}");
        // passa argumentos
        if (read.ReadType == Type.String)
        {
            // a0 eh o endereco
            // a1 eh o tamanho maximo. hardcoded 256 (255 caracteres + \0)
            
            if (read.Dest.AddressType == ILAddressType.Global)
            {
                sw.WriteLine($"\tla $a0, {read.Dest.Label} // move endereco base da string para $a0");
            }
            else
            {
                // stack
                sw.WriteLine($"\tadd $a0, $sp, {stackAllocator.GetVariableOffset(read.Dest.Name)} // move endereco base da string para $a0");
            }
            // $a0 tem o endereco da string
            sw.WriteLine($"\taddi $a1, $zero, 256 // tamanho maximo da string lida");
        }
        sw.WriteLine($"\tsyscall // faz syscall {syscall}");

        if (read.ReadType == Type.String) return;
        switch (read.Dest.AddressType) {
            // resultado esta no $v0
            case ILAddressType.Temporary:
                sw.WriteLine($"\tadd {registerAllocator.GetRegister(read.Dest)}, $v0, $zero // armazena resultado de leitura em {read.Dest}");
                break;
            case ILAddressType.Global:
                sw.WriteLine($"\tsw $v0, {read.Dest.Label} // armazena resultado de leitura em {read.Dest.Name}");
                break;
            case ILAddressType.Stack:
                // stack
                sw.WriteLine($"\tsw $v0, {stackAllocator.GetVariableOffset(read.Dest.Name)}($sp) // armazena resultado de leitura em {read.Dest}");
                break;
        }
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
            if (index == -1) {
                return -1;
            }
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
        private Stack<string> freeRegisters;

        public RegisterAllocator()
        {
            freeRegisters = new Stack<string>(new[] {
                "$t0", "$t1", "$t2", "$t3", "$t4", "$t5", "$t6", "$t7", "$t8", "$t9"
            }.Reverse());
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
            
            string reg = freeRegisters.Pop();
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
            freeRegisters.Push(register);
        }
        
        public List<string> GetUsedRegisters()
        {
            return tempToRegister.Values.ToList();
        }
    }
    
    private string EscapeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.StartsWith('"') && value.EndsWith('"')) {
            value = value.Substring(1, value.Length - 2);
        }

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
                case '\\': result.Append(@"\"); break;
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
                    if (ilBinaryOp.Left.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilBinaryOp.Left);
                    }
                    if (ilBinaryOp.Right.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilBinaryOp.Right);
                    }
                    if (ilBinaryOp.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilBinaryOp.Dest);
                    }
                    break;
                case ILCall ilCall:
                    foreach (ILAddress arg in ilCall.Arguments)
                    {
                        if (arg.AddressType == ILAddressType.Temporary)
                        {
                            temporaries.Add(arg);
                        }
                    }
                    if(ilCall.Dest is not null && ilCall.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilCall.Dest);
                    }
                    break;
                case ILGoto ilGoto:
                    break;
                case ILIfGoto ilIfGoto:
                    if (ilIfGoto.Condition.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilIfGoto.Condition);
                    }
                    break;
                case ILLabel ilLabel:
                    break;
                case ILLoad ilLoad:
                    if (ilLoad.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilLoad.Dest);
                    }
                    break;
                case ILMove ilMove:
                    if (ilMove.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilMove.Dest);
                    }
                    if (ilMove.Src.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilMove.Src);
                    }
                    break;
                case ILRead ilRead:
                    if (ilRead.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilRead.Dest);
                    }
                    break;
                case ILReturn ilReturn:
                    if (ilReturn.Value is not null && ilReturn.Value.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilReturn.Value);
                    }
                    break;
                case ILUnaryOp ilUnaryOp:
                    if (ilUnaryOp.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilUnaryOp.Dest);
                    }
                    if (ilUnaryOp.Operand.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilUnaryOp.Operand);
                    }
                    break;
                case ILVar ilVar:
                    break;
                case ILWrite ilWrite:
                    if (ilWrite.Src.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilWrite.Src);
                    }
                    break;
                case ILLoadPtr ilLoadPtr:
                    if (ilLoadPtr.Dest.AddressType == ILAddressType.Temporary)
                    {
                        temporaries.Add(ilLoadPtr.Dest);
                    }
                    if (ilLoadPtr.Src.AddressType == ILAddressType.Temporary)
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