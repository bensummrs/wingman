using Microsoft.Extensions.AI;

namespace Wingman.Cli;

public static class ChatResponseText
{
    public static string ExtractText(ChatResponse response)
    {
        var text = response.Text;
        return string.IsNullOrWhiteSpace(text) ? response.ToString() ?? string.Empty : text.TrimEnd();
    }
}

