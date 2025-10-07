namespace FortallCompiler.Steps;

public interface IAssembler<in TIn, out TOut> {
    
    public TOut Assemble(TIn input);
}