using System.Text;

namespace FortallCompiler.Il;

public readonly struct ClassReference {
    public string? Assembly { get; init; }
    public string? Namespace { get; init; }
    public string Name { get; init; }

    public override string ToString() {
        StringBuilder sb = new();

        if (Assembly is not null) {
            sb.Append('[');
            sb.Append(Assembly);
            sb.Append(']');
        }

        if (Namespace is not null) {
            sb.Append(Namespace);
            sb.Append('.');
        }
        sb.Append(Name);

        return sb.ToString();
    }
}