namespace FortallCompiler;

public static class Utils {
    
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action) {
        foreach (T item in source) {
            action(item);
        }
    }
}