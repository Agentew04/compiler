namespace FortallCompiler.Fil;


public class ILIfGoto : ILInstruction
{
    public ILAddress Condition;
    public string Label;

    public ILIfGoto(ILAddress condition, string label)
    {
        Condition = condition;
        Label = label;
    }
    
    public override string ToString() => $"if {Condition} goto {Label}";
}