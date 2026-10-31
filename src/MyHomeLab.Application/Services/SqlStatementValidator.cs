using System.Text;
using MyHomeLab.Application.Errors;

namespace MyHomeLab.Application.Services;

/// <summary>
/// Gatekeeper for the SQL workbench: exactly one statement per run, and only DML
/// (SELECT/INSERT/UPDATE/DELETE/WITH). Scans the raw text comment-, string- and
/// dollar-quote-aware so a semicolon hidden in a literal can never smuggle in a
/// second statement.
/// </summary>
public static class SqlStatementValidator
{
    private const int MaxSqlLength = 100_000;

    private static readonly string[] AllowedCommands = ["SELECT", "INSERT", "UPDATE", "DELETE", "WITH"];
    private static readonly string[] QueryCommands = ["SELECT", "INSERT", "UPDATE", "DELETE"];

    public static string Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ValidationException("Enter a SQL statement.");
        }

        if (sql.Length > MaxSqlLength)
        {
            throw new ValidationException($"Statement is too long (limit {MaxSqlLength} characters).");
        }

        var statements = SplitStatements(sql);
        if (statements.Count == 0)
        {
            throw new ValidationException("No SQL statement found in the input.");
        }

        if (statements.Count > 1)
        {
            throw new ValidationException("Run one statement at a time.");
        }

        var statement = statements[0];
        var first = FirstWord(statement);
        if (first.Length == 0)
        {
            throw new ValidationException("Enter a SQL statement.");
        }

        var isWith = first.Equals("WITH", StringComparison.OrdinalIgnoreCase);
        var command = isWith ? FindTopLevelCommand(statement) : first.ToUpperInvariant();

        if (!AllowedCommands.Contains(command, StringComparer.Ordinal))
        {
            throw new ValidationException($"Only {string.Join(", ", AllowedCommands)} statements are allowed in the workbench.");
        }

        // A leading WITH can only ever feed SELECT/INSERT/UPDATE/DELETE, so any
        // INTO that survives at the top level of a SELECT main clause means the
        // statement creates a table. Data-modifying CTEs keep their INTO inside
        // the CTE parentheses, which is why WITH only checks depth zero.
        if (command == "SELECT" && ContainsWord(statement, "INTO", topLevelOnly: isWith))
        {
            throw new ValidationException("SELECT ... INTO is not allowed; use INSERT INTO instead.");
        }

        return command;
    }

    private static List<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(i + 2, sql.Length);
                continue;
            }

            if (c == '$' && (i == 0 || !IsWordChar(sql[i - 1])))
            {
                var tagEnd = sql.IndexOf('$', i + 1);
                if (tagEnd > i && IsValidDollarTag(sql[(i + 1)..tagEnd]))
                {
                    var tag = sql[i..(tagEnd + 1)];
                    var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        throw new ValidationException("Unterminated dollar-quoted string.");
                    }

                    i = close + tag.Length;
                    current.Append(' ');
                    continue;
                }
            }

            if (c == '\'')
            {
                var escaped = i > 0 && (sql[i - 1] == 'E' || sql[i - 1] == 'e');
                i = SkipQuoted(sql, i + 1, escaped, '\'');
                current.Append(' ');
                continue;
            }

            if (c == '"')
            {
                i = SkipQuoted(sql, i + 1, escapes: false, '"');
                current.Append(' ');
                continue;
            }

            if (c == ';')
            {
                statements.Add(current.ToString());
                current.Clear();
                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        statements.Add(current.ToString());
        return statements.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static int SkipQuoted(string sql, int i, bool escapes, char quote)
    {
        while (i < sql.Length)
        {
            var c = sql[i];
            if (escapes && c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        throw new ValidationException(quote == '"' ? "Unterminated quoted identifier." : "Unterminated string literal.");
    }

    private static bool IsValidDollarTag(string tag)
    {
        if (tag.Length == 0)
        {
            return true;
        }

        if (char.IsAsciiDigit(tag[0]))
        {
            return false;
        }

        return tag.All(IsWordChar);
    }

    private static string FindTopLevelCommand(string statement)
    {
        var depth = 0;
        var i = 0;

        while (i < statement.Length)
        {
            var c = statement[i];
            if (c == '(')
            {
                depth++;
                i++;
                continue;
            }

            if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
                i++;
                continue;
            }

            if (!IsWordChar(c))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < statement.Length && IsWordChar(statement[i]))
            {
                i++;
            }

            if (depth != 0)
            {
                continue;
            }

            var word = statement[start..i];
            foreach (var candidate in QueryCommands)
            {
                if (word.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            // VALUES/MERGE/TABLE would be the main statement here (WITH can only
            // precede these seven keywords), so stop rather than scanning into a
            // MERGE's INSERT clause and misreporting the command.
            if (word.Equals("VALUES", StringComparison.OrdinalIgnoreCase)
                || word.Equals("MERGE", StringComparison.OrdinalIgnoreCase)
                || word.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        throw new ValidationException("Unsupported statement after WITH; only SELECT, INSERT, UPDATE or DELETE are allowed.");
    }

    private static bool ContainsWord(string statement, string word, bool topLevelOnly)
    {
        var depth = 0;
        var i = 0;

        while (i < statement.Length)
        {
            var c = statement[i];
            if (c == '(')
            {
                depth++;
                i++;
                continue;
            }

            if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
                i++;
                continue;
            }

            if (!IsWordChar(c))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < statement.Length && IsWordChar(statement[i]))
            {
                i++;
            }

            if ((!topLevelOnly || depth == 0)
                && statement[start..i].Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FirstWord(string statement)
    {
        var i = 0;
        while (i < statement.Length && !IsWordChar(statement[i]))
        {
            i++;
        }

        var start = i;
        while (i < statement.Length && IsWordChar(statement[i]))
        {
            i++;
        }

        return statement[start..i];
    }

    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
