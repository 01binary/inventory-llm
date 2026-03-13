using System.ComponentModel.DataAnnotations;

namespace InventoryDemo.Server.DTOs;

public sealed class SpeakRequest
{
    [Required]
    [MaxLength(5000)]
    public string Text { get; set; } = string.Empty;
}
