namespace FortallCompiler.IL;

public class ILAddress : IComparable<ILAddress>
{
    public string Name { get; }
    
    public ILAddressType AddressType { get; }
    
    public string? Label { get; set; }
    
    public ILAddress(string name, ILAddressType addressType)
    {
        Name = name;
        AddressType = addressType;
    }

    public override string ToString() => Name;

    public int CompareTo(ILAddress? other) {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        int nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
        if (nameComparison != 0) return nameComparison;
        return AddressType.CompareTo(other.AddressType);
    }
}

public enum ILAddressType {
    Temporary,
    Global,
    Stack
}