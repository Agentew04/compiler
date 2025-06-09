namespace FortallCompiler.CodeGeneration.IL;

public class ILReturn : ILInstruction
{
    public string? Value;

    public ILReturn(string? value = null)
    {
        Value = value;
    }
    
    public override string ToString() => Value != null ? $"return {Value}" : $"return";
}