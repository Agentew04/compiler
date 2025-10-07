namespace FortallCompiler.Steps;

/// <summary>
/// Interface that define methods that all optimizers must implement
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IOptimizer<T> {
    
    /// <summary>
    /// Optimizes the given input. Modifies the input in place.
    /// </summary>
    /// <param name="input"></param>
    public void Optimize(T input);
}