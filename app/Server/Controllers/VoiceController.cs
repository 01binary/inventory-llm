using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/voice")]
public sealed class VoiceController : ControllerBase
{
    private readonly SpeechToTextProxyService _speechToTextProxyService;
    private readonly TextToSpeechService _textToSpeechService;

    public VoiceController(
        SpeechToTextProxyService speechToTextProxyService,
        TextToSpeechService textToSpeechService)
    {
        _speechToTextProxyService = speechToTextProxyService;
        _textToSpeechService = textToSpeechService;
    }

    [HttpPost("transcribe-proxy")]
    [RequestFormLimits(MultipartBodyLengthLimit = 25_000_000)]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> TranscribeAsync(IFormFile audio)
    {
        if (audio is null || audio.Length == 0)
        {
            return BadRequest(new { message = "Audio file is required." });
        }

        try
        {
            var result = await _speechToTextProxyService.TranscribeAsync(audio);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("speak")]
    public async Task<IActionResult> SpeakAsync([FromBody] SpeakRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var audio = await _textToSpeechService.SynthesizeAsync(request.Text, cancellationToken);
            return File(audio, "audio/wav", "speech.wav");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
