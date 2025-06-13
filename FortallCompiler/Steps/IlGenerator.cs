using System.Text;
using FortallCompiler.Ast;
using FortallCompiler.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class IlGenerator {
    private ProgramNode program;
    private readonly Stack<ILAddress> freeTemps = new();
    private int tempCounter;
    private string mainLabel;
    private readonly Dictionary<string, string> globalLabels = [];
    
    public ILProgram GenerateIlCode(ProgramNode program) {
        this.program = program;
        ILProgram ilProgram = new();

        // add global variables
        foreach (FieldDeclarationNode globalVar in program.TopLevelNodes.Where(x => x is FieldDeclarationNode).Cast<FieldDeclarationNode>())
        {
            ILGlobalVariable ilGlob = new();
            ilGlob.Name = $"__global_{globalVar.FieldName}";
            globalLabels[globalVar.FieldName] = ilGlob.Name;
            ilGlob.Type = globalVar.FieldType;
            ilGlob.Value = globalVar.InitValue?.Value;
            ilProgram.Globals.Add(ilGlob);
        }
        
        // add string literals as global variables
        foreach (var strLitKvp in program.StringLiterals)
        {
            ILGlobalVariable ilGlob = new()
            {
                Name = strLitKvp.Key,
                Type = Type.String,
                Value = strLitKvp.Value
            };
            ilProgram.Globals.Add(ilGlob);
        }
        
        // add functions
        foreach (FunctionNode function in program.TopLevelNodes.Where(x => x is FunctionNode).Cast<FunctionNode>())
        {
            ilProgram.Functions.Add(GenerateFunction(function));
        }
        
        ilProgram.MainLabel = mainLabel;
        
        return ilProgram;
    }

    private ILFunction GenerateFunction(FunctionNode function)
    {
        List<string> parameters = [];
        parameters.AddRange(function.Parameters.Select(param => param.Name));
        ILFunction ilFunction = new(function.Name, parameters);
        List<ILInstruction> instructions = [];
        ilFunction.Instructions = instructions;
        string label = "__function_" + function.Name;
        if (function.Name == "main")
        {
            mainLabel = label;
        }

        ilFunction.Label = label;
        instructions.Add(new ILLabel(label));

        int idx = 0;
        
        foreach (StatementNode stmt in function.Body.Statements)
        {
            Generate(stmt, instructions, $"__function_{function.Name}", ref idx, function.Body.ScopeData!);
        }
        
        if (instructions.Count == 0 || instructions.Last() is not ILReturn)
        {
            instructions.Add(new ILReturn());
        }
        return ilFunction;
    }

    private void Generate(StatementNode statement, List<ILInstruction> instructions, string namespaceName, ref int idx, SemanticAnalyzer.ScopeData scopeData = null!)
    {
        switch (statement)
        {
            case ReturnStatementNode returnStmt:
                if (returnStmt.Expression is null)
                {
                    instructions.Add(new ILReturn());
                    return;
                }
                ILAddress returnValue = Generate(returnStmt.Expression, instructions);
                instructions.Add(new ILReturn(returnValue));
                ReleaseTemp(returnValue);
                break;
            case IfStatementNode ifStmt:
                ILAddress ifCondTemp = Generate(ifStmt.Condition, instructions);
                string elseLabel = $"{namespaceName}_else_{idx++}";
                string endIfLabel = $"{namespaceName}_endif_{idx++}";
                ILAddress notCondTemp = GetTemp();
                instructions.Add(new ILUnaryOp(notCondTemp, UnaryOperationType.Not, ifCondTemp));
                ReleaseTemp(ifCondTemp);
                instructions.Add(new ILIfGoto(notCondTemp, elseLabel));
                ReleaseTemp(notCondTemp);

                int ifIdx = 0;
                foreach (StatementNode s in ifStmt.ThenBlock.Statements)
                {
                    Generate(s, instructions, namespaceName + $"_if{idx-1}then", ref ifIdx);
                }
                
                instructions.Add(new ILGoto(endIfLabel));
                instructions.Add(new ILLabel(elseLabel));
                if (ifStmt.ElseBlock != null)
                {
                    foreach (StatementNode s in ifStmt.ElseBlock.Statements)
                    {
                        Generate(s, instructions, namespaceName + $"_if{idx-1}else", ref ifIdx);
                    }
                }
                instructions.Add(new ILLabel(endIfLabel));
                break;
            case WhileStatementNode whileStmt:
                string startLabel = $"{namespaceName}_while_start_{idx++}";
                string endLabel = $"{namespaceName}_while_end_{idx++}";
                
                instructions.Add(new ILLabel(startLabel));
                
                ILAddress whileCondTemp = Generate(whileStmt.Condition, instructions);
                ILAddress whileNotCondTemp = GetTemp();
                instructions.Add(new ILUnaryOp(whileNotCondTemp, UnaryOperationType.Not, whileCondTemp));
                ReleaseTemp(whileCondTemp);
                instructions.Add(new ILIfGoto(whileNotCondTemp, endLabel));
                ReleaseTemp(whileNotCondTemp);

                foreach (StatementNode stmt in whileStmt.Body.Statements)
                {
                    Generate(stmt, instructions, namespaceName + $"_while{idx-1}body", ref idx);
                }
                
                instructions.Add(new ILGoto(startLabel));
                instructions.Add(new ILLabel(endLabel));
                break;
            case AssignmentNode assignStmt:
                ILAddress valueTemp = Generate(assignStmt.AssignedValue, instructions);
                ILAddressType type = program.TopLevelNodes.OfType<FieldDeclarationNode>()
                    .Any(x => x.FieldName == assignStmt.VariableName)
                    ? ILAddressType.Global
                    : ILAddressType.Stack;
                ILAddress addr = new ILAddress(assignStmt.VariableName, type);
                if (addr.AddressType == ILAddressType.Global) {
                    addr.Label = globalLabels[addr.Name];
                }
                    
                if (assignStmt.AssignedValue.ExpressionType == Type.String)
                {
                    instructions.Add(new ILLoadPtr(addr, valueTemp));
                }
                else
                {
                    instructions.Add(new ILMove(addr, valueTemp));
                }
                ReleaseTemp(valueTemp);
                break;
            case WriteNode writeStmt:
                ILAddress writeTemp = Generate(writeStmt.Expression, instructions);
                instructions.Add(new ILWrite(writeTemp, writeStmt.Expression.ExpressionType));
                ReleaseTemp(writeTemp);
                break;
            case ReadNode readStmt:
                instructions.Add(new ILRead(new ILAddress(readStmt.VariableName, ILAddressType.Stack), scopeData.GetVariable(readStmt.VariableName)!.Type));
                break;
            case FunctionCallStatementNode funcCallStmt:
                FunctionCallExpressionNode funcCall = funcCallStmt.FunctionCallExpression;
                List<ILAddress> args = [];
                args.AddRange(funcCall.Arguments.Select(arg => Generate(arg, instructions)));
                ILAddress tCall = GetTemp();
                instructions.Add(new ILCall(tCall, funcCall.FunctionName, args));
                foreach (ILAddress arg in args)
                {
                    ReleaseTemp(arg);
                }
                // como eh um statement, nao precisamos guardar o retorno da func.
                ReleaseTemp(tCall);
                break;
            case VariableDeclarationNode varDecl:
                instructions.Add(new ILVar(varDecl.VariableName, varDecl.VariableType));
                if (varDecl.InitValue is null)
                {
                    break;
                }
                ILAddress initValueTemp = Generate(varDecl.InitValue, instructions);
                if (varDecl.InitValue.ExpressionType == Type.String)
                {
                    instructions.Add(new ILLoadPtr(new ILAddress(varDecl.VariableName, ILAddressType.Stack), initValueTemp));
                }
                else
                {
                    instructions.Add(new ILMove(new ILAddress(varDecl.VariableName, ILAddressType.Stack), initValueTemp));
                }
                ReleaseTemp(initValueTemp);
                break;
        }
    }

    private ILAddress Generate(ExpressionNode expression, List<ILInstruction> instructions)
    {
        switch (expression) {
            case LiteralExpressionNode lit:
                if (lit.Type == Type.String)
                {
                    return new ILAddress(lit.StringIdentifier!, ILAddressType.Global);
                }
                ILAddress tLit = GetTemp();
                instructions.Add(new ILLoad(tLit, lit.Value));
                return tLit;
            case IdentifierExpressionNode identifier:
                ILAddressType type = program.TopLevelNodes.OfType<FieldDeclarationNode>()
                    .Any(x => x.FieldName == identifier.Name)
                    ? ILAddressType.Global
                    : ILAddressType.Stack;
                ILAddress addr = new ILAddress(identifier.Name, type);
                if (addr.AddressType == ILAddressType.Global) {
                    addr.Label = globalLabels[addr.Name];
                }
                return addr;
            case UnaryExpressionNode un:
                ILAddress tUnary = GetTemp();
                ILAddress operand = Generate(un.Operand, instructions);
                instructions.Add(new ILUnaryOp(tUnary, un.Operation, operand));
                ReleaseTemp(operand);
                return tUnary;
            case BinaryExpressionNode bin:
                ILAddress tBinary = GetTemp();
                ILAddress left = Generate(bin.Left, instructions);
                ILAddress right = Generate(bin.Right, instructions);
                instructions.Add(new ILBinaryOp(tBinary, left, bin.Operation, right));
                ReleaseTemp(left);
                ReleaseTemp(right);
                return tBinary;
            case FunctionCallExpressionNode call:
                List<ILAddress> args = [];
                args.AddRange(call.Arguments.Select(arg => Generate(arg, instructions)));
                ILAddress tCall = GetTemp();
                instructions.Add(new ILCall(tCall, call.FunctionName, args));
                foreach (ILAddress arg in args)
                {
                    ReleaseTemp(arg);
                }
                return tCall;
            default:
                throw new NotSupportedException($"Unhandled expr: {expression.GetType().Name}");
        }
    }

    private ILAddress GetTemp()
    {
        if (freeTemps.Count > 0)
        {
            return freeTemps.Pop();
        }
        else
        {
            return new ILAddress($"t{tempCounter++}", ILAddressType.Temporary);
        }
    }
    
    private void ReleaseTemp(ILAddress temp)
    {
        if(temp.AddressType != ILAddressType.Temporary)
        {
            return;
        }
        freeTemps.Push(temp);
    }
}