namespace FortallCompiler.Il;

public enum Accessibility {
    Private,
    Protected,
    Public,
    Internal
}
public static class AccessibilityExtensions {
    public static string ToIlString(this Accessibility access) {
        return access switch {
            Accessibility.Private => "private",
            Accessibility.Protected => "family",
            Accessibility.Public => "public",
            Accessibility.Internal => "assembly",
            _ => throw new ArgumentOutOfRangeException(nameof(access), access, null)
        };
    }
}