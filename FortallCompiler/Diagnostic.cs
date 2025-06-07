namespace FortallCompiler;

public class Diagnostic {
    public Diagnostic(string message, int line, int column) {
        Message = message;
        Line = line;
        Column = column;
    }

    public string Message { get; }
    public int Line { get; }
    public int Column { get; }

    public override string ToString() {
        return $"{Line}:{Column} - {Message}";
    }
    
}