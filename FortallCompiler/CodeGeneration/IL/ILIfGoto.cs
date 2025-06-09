namespace FortallCompiler.CodeGeneration.IL;


public class ILIfGoto : ILInstruction
{
    public string Condition;
    public string Label;

    public ILIfGoto(string condition, string label)
    {
        Condition = condition;
        Label = label;
    }
    
    public override string ToString() => $"if {Condition} goto {Label}";
}