using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly LlmService _llmService;

    public ChatController(LlmService llmService)
    {
        _llmService = llmService;
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteAsync([FromBody] ChatCompletionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _llmService.CompleteAsync(request);
        return Ok(result);
    }
}
