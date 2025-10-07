using FortallCompiler.Ast;

namespace FortallCompiler.Fil;

public class ILUnaryOp : ILInstruction
{
    public ILAddress Dest, Operand;
    public UnaryOperationType Op;

    public ILUnaryOp(ILAddress dest, UnaryOperationType op, ILAddress operand)
    {
        Dest = dest;
        Op = op;
        Operand = operand;
    }

    public override string ToString() => $"unaryop {Dest} = {Op} {Operand}";
}