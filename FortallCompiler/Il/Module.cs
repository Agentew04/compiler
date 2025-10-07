using System.Text;

namespace FortallCompiler.Il;

public readonly struct Module {
    public string Name { get; init; }

    public override string ToString() {
        StringBuilder sb = new();
        sb.AppendLine($".module {Name}");
        return sb.ToString();
    }
}