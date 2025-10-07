using System.Text;

namespace FortallCompiler.Il;

public readonly struct Type {
    
    public bool IsPrimitive { get; init; }
    
    public bool IsValueType { get; init; }
    
    public ClassReference Reference { get; init; }

    public override string ToString() {
        StringBuilder sb = new();

        if (!IsPrimitive) {
            sb.Append(IsValueType ? "valuetype " : "class ");
        }
        
        sb.Append(Reference.ToString());

        return sb.ToString();
    }
    
    public static readonly Type Int32 = new() {
        IsPrimitive = true,
        IsValueType = true,
        Reference = new ClassReference {
            Name = "int32",
        }
    };
    public static readonly Type Void = new() {
        IsPrimitive = true,
        IsValueType = true,
        Reference = new ClassReference {
            Name = "void",
        }
    };

    public static readonly Type Boolean = new() {
        IsPrimitive = true,
        IsValueType = true,
        Reference = new ClassReference {
            Name = "bool",
        }
    };
    
    public static readonly Type String = new() {
        IsPrimitive = true,
        IsValueType = false,
        Reference = new ClassReference {
            Name = "string",
        }
    };
}