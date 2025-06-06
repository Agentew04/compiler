using Antlr4.Runtime;

namespace FortallCompiler;

class SyntaxErrorListener : IAntlrErrorListener<IToken>
{
    public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg,
        RecognitionException e)
    {
        Console.WriteLine($"[linha {line}:{charPositionInLine}] erro de sintaxe: {msg}");
    }
}