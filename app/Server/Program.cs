using System.Net.Http.Headers;
using InventoryDemo.Server.Data;
using InventoryDemo.Server.Options;
using InventoryDemo.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppPathsOptions>(builder.Configuration.GetSection(AppPathsOptions.SectionName));
builder.Services.Configure<ModelServiceOptions>(builder.Configuration.GetSection(ModelServiceOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools<InventoryMcpTools>();

builder.Services.AddHttpClient("llm", client =>
{
    var baseUrl = builder.Configuration["ModelServices:LlmBaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromMinutes(3);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient("stt", client =>
{
    var baseUrl = builder.Configuration["ModelServices:WhisperBaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<DiagnosticsService>();
builder.Services.AddScoped<LlmService>();
builder.Services.AddScoped<SpeechToTextProxyService>();
builder.Services.AddScoped<TextToSpeechService>();
builder.Services.AddScoped<AppConfigService>();
builder.Services.AddSingleton<SystemPromptService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();

    var systemPromptService = scope.ServiceProvider.GetRequiredService<SystemPromptService>();
    systemPromptService.GetSystemPrompt();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is not null)
        {
            logger.LogError(feature.Error, "Unhandled exception for request {Path}", context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "An unexpected server error occurred." });
    });
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapMcp("/mcp");
app.MapFallbackToFile("index.html");

app.Run();
