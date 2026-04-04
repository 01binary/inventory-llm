namespace InventoryDemo.Server.DTOs;

public sealed class ChatCompletionRequest
{
    public string Prompt { get; set; } = string.Empty;

    public IReadOnlyList<ChatMessageDto>? Messages { get; set; }

    public int MaxTokens { get; set; } = 128;
}

public sealed class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
