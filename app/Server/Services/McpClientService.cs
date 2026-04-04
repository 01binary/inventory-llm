using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class McpClientService
{
    private readonly ModelServiceOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientService> _logger;

    public McpClientService(
        IOptions<ModelServiceOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<McpClientService> logger)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
    {
        return await WithClientAsync(async client =>
        {
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return tools.Select(tool => new McpToolDescriptor(
                    tool.Name,
                    tool.Description,
                    JsonDocument.Parse(tool.JsonSchema.GetRawText()).RootElement.Clone()))
                .ToList();
        }, cancellationToken);
    }

    public async Task<string> CallToolAsync(
        string toolName,
        string? argumentsJson,
        CancellationToken cancellationToken)
    {
        return await WithClientAsync(async client =>
        {
            var arguments = ParseArguments(argumentsJson);
            _logger.LogInformation("Calling MCP tool {ToolName} at {McpServerUrl}", toolName, _options.McpServerUrl);
            var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
            return FormatToolResult(result);
        }, cancellationToken);
    }

    private async Task<T> WithClientAsync<T>(
        Func<McpClient, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(_options.McpServerUrl, UriKind.Absolute);
        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = endpoint
        };

        await using var transport = new HttpClientTransport(transportOptions, _loggerFactory);
        await using var client = await McpClient.CreateAsync(
            transport,
            loggerFactory: _loggerFactory,
            cancellationToken: cancellationToken);

        return await operation(client);
    }

    private static IReadOnlyDictionary<string, object?> ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson)
                ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string FormatToolResult(ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.StructuredContent.HasValue)
        {
            return result.StructuredContent.Value.GetRawText();
        }

        if (result.Content is { Count: > 0 })
        {
            return JsonSerializer.Serialize(result.Content);
        }

        return JsonSerializer.Serialize(new
        {
            isError = result.IsError ?? false
        });
    }
}

public sealed record McpToolDescriptor(string Name, string Description, JsonElement JsonSchema);
