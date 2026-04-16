using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryDemo.Server.DTOs;

public sealed class LlmCompletionResponseDto
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<LlmChoiceDto>? Choices { get; set; }
}

public sealed class LlmChoiceDto
{
    [JsonPropertyName("message")]
    public LlmAssistantMessageDto? Message { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class LlmAssistantMessageDto
{
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<LlmToolCallDto>? ToolCalls { get; set; }
}

public sealed class LlmToolCallDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public LlmToolFunctionDto? Function { get; set; }
}

public sealed class LlmToolFunctionDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

public sealed class LlmModelListResponseDto
{
    [JsonPropertyName("data")]
    public IReadOnlyList<LlmModelDto>? Data { get; set; }
}

public sealed class LlmModelDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public sealed class LlmConversationMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LlmToolCallDto>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }
}
