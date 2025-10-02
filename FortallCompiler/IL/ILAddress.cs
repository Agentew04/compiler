using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler.IL;

public class ILAddress : IEquatable<ILAddress>
{
    public string Name { get; }
    
    public ILAddressType AddressType { get; }
    
    public string? Label { get; set; }
    
    public Type Type { get; }
    
    public ILAddress(string name, ILAddressType addressType, Type type)
    {
        Name = name;
        AddressType = addressType;
        Type = type;
    }

    public override string ToString() => Name;

    public bool Equals(ILAddress? other) {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && AddressType == other.AddressType && Label == other.Label;
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ILAddress)obj);
    }

    public override int GetHashCode() {
        return HashCode.Combine(Name, (int)AddressType, Label);
    }
}

public enum ILAddressType {
    Temporary,
    Global,
    Stack,
    Parameter
}