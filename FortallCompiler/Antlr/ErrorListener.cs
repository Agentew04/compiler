using Antlr4.Runtime;

namespace FortallCompiler.Antlr;

class SyntaxErrorListener : IAntlrErrorListener<IToken>
{
    public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg,
        RecognitionException e)
    {
        OnError?.Invoke(line, charPositionInLine, msg);
    }

    public event Action<int, int, string>? OnError;
}