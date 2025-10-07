namespace FortallCompiler.Steps;

public interface IGenerator<in TIn, out TOut> {

    public TOut Generate(TIn input);
}