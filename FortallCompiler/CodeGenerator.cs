using System.Linq.Expressions;
using System.Net.NetworkInformation;
using FortallCompiler.Ast;
using FortallCompiler.CodeGeneration.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class CodeGenerator
{
    ProgramNode program;

    private Stack<string> freeTemps = new();
    private int tempCounter = 0;
    // usado para as labels nos ifs e outros statements
    private string currentFunctionName = "";
    
    public ILProgram GenerateIlCode(ProgramNode program)
    {
        this.program = program;
        ILProgram ilProgram = new();

        // add global variables
        foreach (FieldDeclarationNode globalVar in program.TopLevelNodes.Where(x => x is FieldDeclarationNode).Cast<FieldDeclarationNode>())
        {
            ILGlobalVariable ilGlob = new();
            ilGlob.Name = $"_globalvariable__{globalVar.FieldName}";
            ilGlob.Type = globalVar.FieldType;
            ilGlob.Value = globalVar.InitValue?.Value;
            ilProgram.Globals.Add(ilGlob);
        }
        
        // add functions
        foreach (FunctionNode function in program.TopLevelNodes.Where(x => x is FunctionNode).Cast<FunctionNode>())
        {
            ilProgram.Functions.Add(GenerateFunction(function));
        }
        
        return ilProgram;
    }

    private ILFunction GenerateFunction(FunctionNode function)
    {
        List<string> parameters = [];
        parameters.AddRange(function.Parameters.Select(param => param.Name));
        ILFunction ilFunction = new(function.Name, parameters);
        
        List<ILInstruction> instructions = [];
        ilFunction.Instructions = instructions;
        instructions.Add(new ILLabel("_function__" + function.Name));

        int idx = 0;
        
        foreach (StatementNode stmt in function.Body.Statements)
        {
            Generate(stmt, instructions, $"_function__{function.Name}", ref idx);
        }

        if (instructions.Count == 0 || instructions.Last() is not ILReturn)
        {
            instructions.Add(new ILReturn());
        }
        return ilFunction;
    }

    private void Generate(StatementNode statement, List<ILInstruction> instructions, string namespaceName, ref int idx)
    {
        switch (statement)
        {
            case ReturnStatementNode returnStmt:
                if (returnStmt.Expression is null)
                {
                    instructions.Add(new ILReturn());
                    return;
                }
                string returnValue = Generate(returnStmt.Expression, instructions);
                instructions.Add(new ILReturn(returnValue));
                ReleaseTemp(returnValue);
                break;
            case IfStatementNode ifStmt:
                string ifCondTemp = Generate(ifStmt.Condition, instructions);
                string elseLabel = $"{namespaceName}_else_{idx++}";
                string endIfLabel = $"{namespaceName}_endif_{idx++}";
                string notCondTemp = GetTemp();
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
                
                string whileCondTemp = Generate(whileStmt.Condition, instructions);
                string whileNotCondTemp = GetTemp();
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
                string valueTemp = Generate(assignStmt.AssignedValue, instructions);
                instructions.Add(new ILMove(assignStmt.VariableName, valueTemp));
                ReleaseTemp(valueTemp);
                break;
            case WriteNode writeStmt:
                string writeTemp = Generate(writeStmt.Expression, instructions);
                instructions.Add(new ILWrite(writeTemp, writeStmt.Expression.ExpressionType));
                ReleaseTemp(writeTemp);
                break;
            case ReadNode readStmt:
                instructions.Add(new ILRead(readStmt.VariableName));
                break;
            case FunctionCallStatementNode funcCallStmt:
                FunctionCallExpressionNode funcCall = funcCallStmt.FunctionCallExpression;
                List<string> args = [];
                args.AddRange(funcCall.Arguments.Select(arg => Generate(arg, instructions)));
                string tCall = GetTemp();
                instructions.Add(new ILCall(tCall, funcCall.FunctionName, args));
                foreach (string arg in args)
                {
                    ReleaseTemp(arg);
                }
                // como eh um statement, nao precisamos guardar o retorno da func.
                ReleaseTemp(tCall);
                break;
            case VariableDeclarationNode varDecl:
                if (varDecl.InitValue is null)
                {
                    break;
                }
                string initValueTemp = Generate(varDecl.InitValue, instructions);
                instructions.Add(new ILMove(varDecl.VariableName, initValueTemp));
                ReleaseTemp(initValueTemp);
                break;
            // TODO: outros statements
        }
    }

    private string Generate(ExpressionNode expression, List<ILInstruction> instructions)
    {
        switch (expression) {
            case LiteralExpressionNode lit:
                string tLit = GetTemp();
                instructions.Add(new ILLoad(tLit, lit.Value));
                return tLit;
            case IdentifierExpressionNode identifier:
                return identifier.Name;
            case UnaryExpressionNode un:
                string tUnary = GetTemp();
                string operand = Generate(un.Operand, instructions);
                instructions.Add(new ILUnaryOp(tUnary, un.Operation, operand));
                ReleaseTemp(operand);
                return tUnary;
            case BinaryExpressionNode bin:
                string tBinary = GetTemp();
                string left = Generate(bin.Left, instructions);
                string right = Generate(bin.Right, instructions);
                instructions.Add(new ILBinaryOp(tBinary, left, bin.Operation, right));
                ReleaseTemp(left);
                ReleaseTemp(right);
                return tBinary;
            case FunctionCallExpressionNode call:
                List<string> args = [];
                args.AddRange(call.Arguments.Select(arg => Generate(arg, instructions)));
                string tCall = GetTemp();
                instructions.Add(new ILCall(tCall, call.FunctionName, args));
                foreach (string arg in args)
                {
                    ReleaseTemp(arg);
                }
                return tCall;
            default:
                throw new NotSupportedException($"Unhandled expr: {expression.GetType().Name}");
        }
    }

    private string GetTemp()
    {
        if (freeTemps.Count > 0)
        {
            return freeTemps.Pop();
        }

        return $"t{tempCounter++}";
    }
    
    private void ReleaseTemp(string temp)
    {
        if (IsTemp(temp))
        {
            freeTemps.Push(temp);
        }
        
    }

    private bool IsTemp(string name) => name.StartsWith('t');
}