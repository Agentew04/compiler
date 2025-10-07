namespace FortallCompiler.Fil;

public class ILReturn : ILInstruction
{
    public ILAddress? Value;

    public ILReturn(ILAddress? value = null)
    {
        Value = value;
    }
    
    public override string ToString() => Value != null ? $"return {Value}" : $"return";
}