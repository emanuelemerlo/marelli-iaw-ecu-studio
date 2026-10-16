using System.Globalization;

namespace MarelliIawEcuStudio.Core.Decoding;

/// <summary>
/// A small, side-effect free arithmetic expression used by ECU definitions to
/// turn a raw register value into a physical value.
/// </summary>
/// <remarks>
/// <para>The single variable is <c>x</c>, the raw value read from the ECU.
/// Evaluation uses IEEE double arithmetic in the same operator order as C#,
/// so a formula copied from the original IES2 code yields bit-identical
/// results.</para>
/// <para>Grammar (lowest to highest precedence): <c>a ? b : c</c>,
/// <c>||</c>, <c>&amp;&amp;</c>, <c>== !=</c>, <c>&lt; &lt;= &gt; &gt;=</c>,
/// <c>+ -</c>, <c>* /</c>, unary <c>- !</c>, then numbers, <c>x</c>,
/// parentheses and the functions <c>idiv(a, b)</c> (integer division
/// truncating toward zero), <c>min</c>, <c>max</c>, <c>abs</c>.
/// Comparisons and logical operators yield 1 or 0.</para>
/// </remarks>
public sealed class Expression
{
    private readonly Node _root;

    private Expression(string text, Node root)
    {
        Text = text;
        _root = root;
    }

    /// <summary>The source text of the expression.</summary>
    public string Text { get; }

    /// <summary>Parses an expression, throwing <see cref="FormatException"/> on syntax errors.</summary>
    public static Expression Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(text);
        var root = parser.ParseExpression();
        parser.ExpectEnd();
        return new Expression(text, root);
    }

    /// <summary>Evaluates the expression for raw value <paramref name="x"/>.</summary>
    public double Evaluate(double x) => _root.Eval(x);

    public override string ToString() => Text;

    private abstract class Node
    {
        public abstract double Eval(double x);
    }

    private sealed class Constant(double value) : Node
    {
        public override double Eval(double x) => value;
    }

    private sealed class Variable : Node
    {
        public static readonly Variable Instance = new();
        public override double Eval(double x) => x;
    }

    private sealed class Unary(char op, Node operand) : Node
    {
        public override double Eval(double x)
        {
            var v = operand.Eval(x);
            return op == '-' ? -v : (v == 0 ? 1 : 0);
        }
    }

    private sealed class Binary(string op, Node left, Node right) : Node
    {
        public override double Eval(double x)
        {
            switch (op)
            {
                case "&&": return left.Eval(x) != 0 && right.Eval(x) != 0 ? 1 : 0;
                case "||": return left.Eval(x) != 0 || right.Eval(x) != 0 ? 1 : 0;
            }
            var a = left.Eval(x);
            var b = right.Eval(x);
            return op switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => a / b,
                "<" => a < b ? 1 : 0,
                "<=" => a <= b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                ">=" => a >= b ? 1 : 0,
                "==" => a == b ? 1 : 0,
                "!=" => a != b ? 1 : 0,
                _ => throw new InvalidOperationException(op),
            };
        }
    }

    private sealed class Conditional(Node condition, Node whenTrue, Node whenFalse) : Node
    {
        public override double Eval(double x) => condition.Eval(x) != 0 ? whenTrue.Eval(x) : whenFalse.Eval(x);
    }

    private sealed class Call(string name, Node[] args) : Node
    {
        public override double Eval(double x) => name switch
        {
            "idiv" => Math.Truncate(args[0].Eval(x) / args[1].Eval(x)),
            "min" => Math.Min(args[0].Eval(x), args[1].Eval(x)),
            "max" => Math.Max(args[0].Eval(x), args[1].Eval(x)),
            "abs" => Math.Abs(args[0].Eval(x)),
            _ => throw new InvalidOperationException(name),
        };
    }

    private sealed class Parser(string text)
    {
        private static readonly Dictionary<string, int> Functions = new()
        {
            ["idiv"] = 2,
            ["min"] = 2,
            ["max"] = 2,
            ["abs"] = 1,
        };

        private int _pos;

        public void ExpectEnd()
        {
            SkipSpace();
            if (_pos < text.Length)
                throw Error($"unexpected '{text[_pos]}'");
        }

        public Node ParseExpression()
        {
            var condition = ParseBinary(0);
            if (!TryConsume("?"))
                return condition;
            var whenTrue = ParseExpression();
            if (!TryConsume(":"))
                throw Error("expected ':'");
            var whenFalse = ParseExpression();
            return new Conditional(condition, whenTrue, whenFalse);
        }

        private static readonly string[][] Levels =
        [
            ["||"],
            ["&&"],
            ["==", "!="],
            ["<=", ">=", "<", ">"],
            ["+", "-"],
            ["*", "/"],
        ];

        private Node ParseBinary(int level)
        {
            if (level == Levels.Length)
                return ParseUnary();
            var left = ParseBinary(level + 1);
            while (true)
            {
                var op = Levels[level].FirstOrDefault(TryConsume);
                if (op is null)
                    return left;
                left = new Binary(op, left, ParseBinary(level + 1));
            }
        }

        private Node ParseUnary()
        {
            if (TryConsume("-"))
                return new Unary('-', ParseUnary());
            if (TryConsume("!"))
                return new Unary('!', ParseUnary());
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipSpace();
            if (_pos >= text.Length)
                throw Error("unexpected end of expression");
            var c = text[_pos];
            if (c == '(')
            {
                _pos++;
                var inner = ParseExpression();
                if (!TryConsume(")"))
                    throw Error("expected ')'");
                return inner;
            }
            if (char.IsDigit(c) || c == '.')
                return ParseNumber();
            if (char.IsLetter(c))
            {
                var start = _pos;
                while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] == '_'))
                    _pos++;
                var name = text[start.._pos];
                if (name == "x")
                    return Variable.Instance;
                if (!Functions.TryGetValue(name, out var arity))
                    throw Error($"unknown identifier '{name}'");
                if (!TryConsume("("))
                    throw Error($"expected '(' after {name}");
                var args = new List<Node>();
                if (!TryConsume(")"))
                {
                    do
                        args.Add(ParseExpression());
                    while (TryConsume(","));
                    if (!TryConsume(")"))
                        throw Error("expected ')'");
                }
                if (args.Count != arity)
                    throw Error($"{name} takes {arity} argument(s)");
                return new Call(name, [.. args]);
            }
            throw Error($"unexpected '{c}'");
        }

        private Node ParseNumber()
        {
            var start = _pos;
            while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.'))
                _pos++;
            if (_pos < text.Length && (text[_pos] == 'e' || text[_pos] == 'E'))
            {
                _pos++;
                if (_pos < text.Length && (text[_pos] == '+' || text[_pos] == '-'))
                    _pos++;
                while (_pos < text.Length && char.IsDigit(text[_pos]))
                    _pos++;
            }
            var token = text[start.._pos];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error($"invalid number '{token}'");
            return new Constant(value);
        }

        private bool TryConsume(string token)
        {
            SkipSpace();
            if (string.CompareOrdinal(text, _pos, token, 0, token.Length) != 0)
                return false;
            // Do not split two-character operators: "<" must not match "<=", "!" must not match "!=".
            var next = _pos + token.Length < text.Length ? text[_pos + token.Length] : '\0';
            if (token is "<" or ">" or "!" && next == '=')
                return false;
            _pos += token.Length;
            return true;
        }

        private void SkipSpace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
                _pos++;
        }

        private FormatException Error(string message) =>
            new($"Expression '{text}', position {_pos + 1}: {message}");
    }
}
