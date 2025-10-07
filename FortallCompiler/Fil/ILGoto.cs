namespace FortallCompiler.Fil;

public class ILGoto : ILInstruction
{
    public string Label;

    public ILGoto(string label)
    {
        Label = label;
    }

    public override string ToString() => $"goto {Label}";
}