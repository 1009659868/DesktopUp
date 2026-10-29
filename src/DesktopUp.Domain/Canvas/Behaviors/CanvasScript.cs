using System.Globalization;
using System.Text;

namespace DesktopUp.Domain;

public sealed record CanvasInstruction(string Op, string? Property = null, double Value = 0, int Duration = 0,
    string Easing = "smooth", int Jump = 0);
public sealed record CanvasProgram(IReadOnlyList<CanvasInstruction> Instructions)
{
    public bool ChangesPoster => Instructions.Any(i => i.Op.StartsWith("poster.", StringComparison.Ordinal));
    public bool ChangesDecoration => Instructions.Any(i => i.Op is "set" or "animate");
}

/// <summary>A small, closed grammar. Never handed to a general-purpose evaluator or CLR compiler.</summary>
public static class CanvasScript
{
    public const string Language = "desktopup.behavior/1";
    // WinUI editing may return CR, imported text may use CRLF or LF. They are the
    // same language newline; persist one representation without changing source meaning.
    public static string NormalizeLineEndings(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    public static CanvasProgram Compile(string source)
    {
        if (source is null || Encoding.UTF8.GetByteCount(source) > 8192) throw new ArgumentException("脚本超过8 KiB预算。");
        return new Parser(NormalizeLineEndings(source)).Parse();
    }

    private sealed class Parser(string source)
    {
        private int at, line = 1, depth;
        private readonly List<CanvasInstruction> code = [];
        private string token = "";
        private bool quoted;
        public CanvasProgram Parse()
        {
            Next(); Block(false);
            if (code.Count == 0) Error("至少编写一个动作。");
            return new(Array.AsReadOnly(code.ToArray()));
        }
        private void Block(bool nested)
        {
            if (++depth > 8) Error("条件嵌套超过8层。");
            while (token.Length != 0 && token != "}") Statement();
            if (nested) Take("}"); else if (token.Length != 0) Error("多余的右括号。");
            depth--;
        }
        private void Statement()
        {
            if (token == "if" && !quoted)
            {
                Next(); Take("("); string condition = Identifier();
                if (condition is not ("collapsed" or "expanded")) Error("条件只支持collapsed或expanded。");
                Take(")"); Take("{"); int branch = Add(new("if", condition)); Block(true);
                int end = Add(new("jump")); code[branch] = code[branch] with { Jump = code.Count };
                if (token == "else" && !quoted) { Next(); Take("{"); Block(true); }
                code[end] = code[end] with { Jump = code.Count }; return;
            }
            string op = Identifier(); Take("(");
            switch (op)
            {
                case "poster.toggle": case "poster.collapse": case "poster.expand":
                    Take(")"); Add(new(op)); break;
                case "wait":
                    int delay = Milliseconds(); Take(")"); Add(new(op, Duration: delay)); break;
                case "set": case "animate":
                    string property = String(); Take(","); double value = Number();
                    if (!CanvasBehaviorPolicy.Actions.Contains(property) ||
                        (property == "opacity" ? value is < 0 or > 1 : Math.Abs(value) > (property == "rotation" ? 180 : 256)))
                        Error("属性或值超出动作目录：opacity 0..1，位移±256 DIP，rotation ±180°。");
                    int duration = 0; string easing = "smooth";
                    if (op == "animate")
                    { Take(","); duration = Milliseconds(); Take(","); easing = String(); if (easing is not ("linear" or "smooth")) Error("缓动须为linear或smooth。"); }
                    Take(")"); Add(new(op, property, value, duration, easing)); break;
                default: Error("未知函数；仅允许poster.toggle/collapse/expand、set、animate、wait。"); break;
            }
            Take(";");
        }
        private int Add(CanvasInstruction instruction)
        { if (code.Count >= 128) Error("脚本超过128条指令。"); code.Add(instruction); return code.Count - 1; }
        private void Take(string expected)
        { if (quoted || token != expected) Error("预期“" + expected + "”。"); Next(); }
        private string Identifier()
        { if (quoted || token.Length == 0 || !char.IsAsciiLetter(token[0])) Error("需要函数或条件名。"); string value = token; Next(); return value; }
        private string String()
        { if (!quoted) Error("需要双引号字符串。"); string value = token; Next(); return value; }
        private double Number()
        {
            if (quoted || !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            { Error("需要有限数值。"); return 0; }
            Next(); return value;
        }
        private int Milliseconds()
        { double value = Number(); if (value is < 0 or > 2000 || value != Math.Truncate(value)) Error("时长须为0..2000整数毫秒。"); return (int)value; }
        private void Next()
        {
            quoted = false;
            while (at < source.Length)
            {
                if (char.IsWhiteSpace(source[at])) { if (source[at++] == '\n') line++; continue; }
                if (source[at] == '/' && at + 1 < source.Length && source[at + 1] == '/')
                { while (at < source.Length && source[at] != '\n') at++; continue; }
                break;
            }
            if (at == source.Length) { token = ""; return; }
            char c = source[at++];
            if (c == '"')
            {
                quoted = true; int start = at;
                while (at < source.Length && source[at] != '"')
                { if (source[at] is '\n' or '\r' or '\\') Error("字符串不支持换行或转义。"); at++; }
                if (at == source.Length) Error("字符串未闭合。");
                token = source[start..at++]; return;
            }
            if ("(){},;".Contains(c)) { token = c.ToString(); return; }
            int begin = at - 1;
            while (at < source.Length && (char.IsAsciiLetterOrDigit(source[at]) || source[at] is '.' or '-' or '+' or '_')) at++;
            token = source[begin..at];
        }
        private void Error(string message) => throw new ArgumentException($"脚本第{line}行：{message}");
    }
}
