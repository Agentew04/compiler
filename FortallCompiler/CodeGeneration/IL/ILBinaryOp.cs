using FortallCompiler.Ast;

namespace FortallCompiler.CodeGeneration.IL;

public class ILBinaryOp : ILInstruction
{
    public string Dest, Left, Right;
    public BinaryOperationType Op;

    public ILBinaryOp(string dest, string left, BinaryOperationType op, string right)
    {
        Dest = dest;
        Left = left;
        Op = op;
        Right = right;
    }
    
    public override string ToString() => $"{Dest} = {Left} {Op.ToString().ToLower()} {Right}";
}