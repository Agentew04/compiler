using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.CodeGeneration.IL;

public class ILAddress : IComparable<ILAddress>
{
    public string Name { get; }
    public bool IsTemporary { get; }
    
    // TODO: setar isso direito no codigo!
    public bool IsGlobal => Name.StartsWith("__global_") || Name.StartsWith("__string_literal_");
    
    public ILAddress(string name, bool isTemporary = false)
    {
        Name = name;
        IsTemporary = isTemporary;
    }

    public override string ToString() => Name;

    public int CompareTo(ILAddress? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        int nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
        if (nameComparison != 0) return nameComparison;
        return IsTemporary.CompareTo(other.IsTemporary);
    }
}