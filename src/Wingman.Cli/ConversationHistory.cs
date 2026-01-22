namespace Wingman.Cli;

public sealed class ConversationHistory
{
    private readonly List<string> lines = [];

    public int Count => lines.Count;

    public void Clear() => lines.Clear();

    public void AddUser(string text) => AddLine($"User: {text}");

    public void AddAssistant(string text) => AddLine($"Assistant: {text}");

    public string ComposePrompt(string userInput)
    {
        if (lines.Count == 0)
            return userInput;

        return $"Conversation so far:\n{string.Join("\n", lines)}\n\nUser: {userInput}";
    }

    private void AddLine(string line)
    {
        lines.Add(line);

        const int maxHistoryLines = 40;
        if (lines.Count > maxHistoryLines)
            lines.RemoveRange(0, lines.Count - maxHistoryLines);
    }
}

