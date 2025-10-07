using FortallCompiler.Ast;

namespace FortallCompiler.Fil;

public class ILBinaryOp : ILInstruction
{
    public ILAddress Dest, Left, Right;
    public BinaryOperationType Op;

    public ILBinaryOp(ILAddress dest, ILAddress left, BinaryOperationType op, ILAddress right)
    {
        Dest = dest;
        Left = left;
        Op = op;
        Right = right;
    }
    
    public override string ToString() => $"binaryOp {Dest} <= {Left} {Op.ToString().ToLower()} {Right}";
}