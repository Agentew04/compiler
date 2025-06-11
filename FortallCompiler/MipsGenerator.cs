using System.Text;
using FortallCompiler.CodeGeneration.IL;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class MipsGenerator
{
    public void Generate(ILProgram program, Stream outputStream)
    {
        using StreamWriter sw = new(outputStream, leaveOpen: true);
        // write globals
        sw.WriteLine(".data");
        foreach (ILGlobalVariable global in program.Globals)
        {
            string type = global.Type switch
            {
                Type.Integer => ".word",
                Type.Boolean => ".byte",
                Type.String => ".asciiz",
                _ => throw new NotSupportedException($"Unsupported global variable type: {global.Type}")
            };
            string initialValue = global.Value switch
            {
                null => "",
                string str => $"\"{EscapeString(str)}\"",
                int i => i.ToString(),
                bool b => b ? "1" : "0",
                _ => throw new NotSupportedException($"Unsupported initial value type: {global.Value.GetType()}")
            };
            sw.WriteLine($"{global.Name}: {type} {initialValue}");
        }
    }
    
    private string EscapeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder result = new(value.Length + value.Length / 2);

        foreach (char c in value)
        {
            switch (c)
            {
                case '\0': result.Append(@"\0"); break;
                case '\a': result.Append(@"\a"); break;
                case '\b': result.Append(@"\b"); break;
                case '\t': result.Append(@"\t"); break;
                case '\n': result.Append(@"\n"); break;
                case '\v': result.Append(@"\v"); break;
                case '\f': result.Append(@"\f"); break;
                case '\r': result.Append(@"\r"); break;
                case '\\': result.Append(@"\\"); break;
                case '"': result.Append(@"\"""); break;
                default:
                    if (c >= ' ')
                        result.Append(c);
                    else // the character is in the 0..31 range
                        result.Append($@"\x{(int)c:X2}");
                    break;
            }
        }

        return result.ToString();
    }
}