using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly LlmService _llmService;
    private readonly SystemPromptService _systemPromptService;

    public ChatController(LlmService llmService, SystemPromptService systemPromptService)
    {
        _llmService = llmService;
        _systemPromptService = systemPromptService;
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteAsync([FromBody] ChatCompletionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var hasPrompt = !string.IsNullOrWhiteSpace(request.Prompt);
        var hasMessages = request.Messages is { Count: > 0 };
        if (!hasPrompt && !hasMessages)
        {
            return BadRequest(new { message = "Provide either prompt or messages." });
        }

        if (request.MaxTokens is < 1 or > 512)
        {
            return BadRequest(new { message = "maxTokens must be between 1 and 512." });
        }

        var result = await _llmService.CompleteAsync(request);
        return Ok(result);
    }

    [HttpGet("system-prompt")]
    public IActionResult GetSystemPrompt() => Ok(new { text = _systemPromptService.GetSystemPrompt() });
}
