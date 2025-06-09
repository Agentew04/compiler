using FortallCompiler.Ast;

namespace FortallCompiler.CodeGeneration.IL;

public class ILUnaryOp : ILInstruction
{
    public string Dest, Operand;
    public UnaryOperationType Op;

    public ILUnaryOp(string dest, UnaryOperationType op, string operand)
    {
        Dest = dest;
        Op = op;
        Operand = operand;
    }

    public override string ToString() => $"{Dest} = {Op} {Operand}";
}