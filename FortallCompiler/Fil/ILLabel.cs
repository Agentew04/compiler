namespace FortallCompiler.Fil;

public class ILLabel : ILInstruction
{
    public string Name;

    public ILLabel(string name)
    {
        Name = name;
    }

    public override string ToString() => $"{Name}:";
}