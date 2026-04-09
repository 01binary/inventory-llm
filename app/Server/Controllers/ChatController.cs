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
    private readonly HelloPromptService _helloPromptService;
    private readonly FewShotPromptService _fewShotPromptService;

    public ChatController(
        LlmService llmService,
        SystemPromptService systemPromptService,
        HelloPromptService helloPromptService,
        FewShotPromptService fewShotPromptService)
    {
        _llmService = llmService;
        _systemPromptService = systemPromptService;
        _helloPromptService = helloPromptService;
        _fewShotPromptService = fewShotPromptService;
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

    [HttpGet("hello-prompt")]
    public IActionResult GetHelloPrompt() => Ok(new { text = _helloPromptService.GetHelloPrompt() });

    [HttpGet("few-shot-prompts")]
    public IActionResult GetFewShotPrompts() => Ok(new { messages = _fewShotPromptService.GetFewShotPrompts() });
}
