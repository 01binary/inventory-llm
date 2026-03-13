using System.ComponentModel.DataAnnotations;

namespace InventoryDemo.Server.DTOs;

public sealed class ChatCompletionRequest
{
    [Required]
    [MaxLength(8000)]
    public string Prompt { get; set; } = string.Empty;

    [Range(1, 512)]
    public int MaxTokens { get; set; } = 128;
}
