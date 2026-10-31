using System.IO.Pipes;
using System.Text.Json;

namespace Gili.ClaudeHook;

/// <summary>
/// Run by Claude Code's hooks as <c>Gili.ClaudeHook.exe &lt;Event&gt;</c> (PRD 9.2, F-CLD-07). Reads the hook's input
/// from stdin, keeps only what Gili shows (never the prompt), and writes one JSON line to \\.\pipe\Gili.Claude. Writes
/// nothing to stdout or stderr and always exits 0, so a missing or slow Gili never changes what Claude does.
/// </summary>
internal static class Program
{
    private const string PipeName = "Gili.Claude";
    private const int ConnectMilliseconds = 30;
    private const int QuestionLength = 200;

    /// <summary>Hook input past this is not read: only PostToolUse carries big tool output, and Gili needs none of it.</summary>
    private const int InputLimit = 16 * 1024 * 1024;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return 0;
            var line = Message(args[0], ReadInput());
            if (line is not null) Send(line);
        }
        catch
        {
            // Whatever goes wrong, Claude carries on as if Gili were not there.
        }
        return 0;
    }

    private static byte[] ReadInput()
    {
        using var stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stdin.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > InputLimit) break;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>The fields of PRD 9.2's hook table, read from the top level without parsing tool output.</summary>
    internal static byte[]? Message(string hookEvent, byte[] input)
    {
        string? session = null, cwd = null, tool = null, type = null, message = null, question = null, error = null;
        var reader = new Utf8JsonReader(input, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;
            ReadFields(ref reader, hookEvent, ref session, ref cwd, ref tool, ref type, ref message, ref question, ref error);
        }
        catch (JsonException)
        {
            // Input cut at the limit: what was read before it still counts.
        }
        if (string.IsNullOrEmpty(session)) return null;

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("event", hookEvent);
            writer.WriteString("session_id", session);
            Write(writer, "cwd", cwd);
            Write(writer, "tool", tool);
            Write(writer, "notification_type", type);
            Write(writer, "message", message);
            Write(writer, "question", question);
            Write(writer, "error", error);
            writer.WriteEndObject();
        }
        output.WriteByte((byte)'\n');
        return output.ToArray();
    }

    private static void ReadFields(ref Utf8JsonReader reader, string hookEvent, ref string? session, ref string? cwd, ref string? tool,
        ref string? type, ref string? message, ref string? question, ref string? error)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            if (!reader.Read()) break;
            switch (name)
            {
                case "session_id": session = Text(ref reader); break;
                case "cwd": cwd = Text(ref reader); break;
                case "tool_name": tool = Text(ref reader); break;
                case "notification_type": type = Text(ref reader); break;
                case "message": message = Text(ref reader); break;
                case "error" when hookEvent == "StopFailure": error = Text(ref reader); break;
                // StopFailure's last_assistant_message is the error Claude showed (with the reset time when there is one);
                // in Stop it is Claude's own answer, which never leaves Claude.
                case "last_assistant_message" when hookEvent == "StopFailure": message = Cut(Text(ref reader)); break;
                case "tool_input" when hookEvent == "PreToolUse":
                    question = FirstQuestion(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
    }

    /// <summary>AskUserQuestion's first question (tool_input.questions[0].question), at most 200 characters.</summary>
    private static string? FirstQuestion(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("questions", out var questions)
            || questions.ValueKind != JsonValueKind.Array
            || questions.GetArrayLength() == 0
            || !questions[0].TryGetProperty("question", out var first)
            || first.ValueKind != JsonValueKind.String) return null;
        var text = first.GetString() ?? "";
        return text.Length > QuestionLength ? text[..QuestionLength] : text;
    }

    private static string? Text(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        reader.Skip();
        return null;
    }

    private static string? Cut(string? text) => text is { Length: > QuestionLength } ? text[..QuestionLength] : text;

    private static void Write(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
    }

    /// <summary>Gili not running means no pipe: the connect gives up after 30 ms.</summary>
    private static void Send(byte[] line)
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        pipe.Connect(ConnectMilliseconds);
        pipe.Write(line, 0, line.Length);
        pipe.Flush();
    }
}
