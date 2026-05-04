using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldBank.Core.Modules.AI.Infrastructure.Services;

public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://goldbank-ollama:11434";
    public string Model { get; set; } = "qwen3-vl";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.0;
}

public sealed class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = default!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = default!;

    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Images { get; set; }
}

public sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = default!;

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaOptions? Options { get; set; }
}

public sealed class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

public sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; } = default!;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

public sealed class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = [];
}

public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send a chat completion request with optional images. Returns the full response.
    /// </summary>
    public async Task<OllamaChatResponse> ChatAsync(
        string systemPrompt,
        string userPrompt,
        List<byte[]>? images = null,
        double? temperature = null,
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new()
            {
                Role = "user",
                Content = userPrompt,
                Images = images?.Select(Convert.ToBase64String).ToList()
            }
        };

        var request = new OllamaChatRequest
        {
            Model = _settings.Model,
            Messages = messages,
            Stream = false,
            Options = new OllamaOptions
            {
                Temperature = temperature ?? _settings.Temperature,
                NumPredict = maxTokens ?? _settings.MaxTokens
            }
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await _httpClient.PostAsJsonAsync(
            $"{_settings.BaseUrl}/api/chat", request, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Ollama");

        sw.Stop();
        _logger.LogInformation(
            "Ollama chat completed: model={Model}, tokens={Tokens}, duration={Duration}ms",
            _settings.Model, result.EvalCount, sw.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Send a chat completion with conversation history. Returns the full response.
    /// </summary>
    public async Task<OllamaChatResponse> ChatWithHistoryAsync(
        string systemPrompt,
        List<OllamaChatMessage> conversationHistory,
        string userMessage,
        double? temperature = null,
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        messages.AddRange(conversationHistory);
        messages.Add(new() { Role = "user", Content = userMessage });

        var request = new OllamaChatRequest
        {
            Model = _settings.Model,
            Messages = messages,
            Stream = false,
            Options = new OllamaOptions
            {
                Temperature = temperature ?? 0.3,
                NumPredict = maxTokens ?? _settings.MaxTokens
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_settings.BaseUrl}/api/chat", request, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Ollama");
    }

    /// <summary>
    /// Stream chat tokens for real-time response delivery (banking assistant).
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string systemPrompt,
        List<OllamaChatMessage> conversationHistory,
        string userMessage,
        double? temperature = null,
        int? maxTokens = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        messages.AddRange(conversationHistory);
        messages.Add(new() { Role = "user", Content = userMessage });

        var request = new OllamaChatRequest
        {
            Model = _settings.Model,
            Messages = messages,
            Stream = true,
            Options = new OllamaOptions
            {
                Temperature = temperature ?? 0.3,
                NumPredict = maxTokens ?? _settings.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/api/chat")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            if (chunk?.Message?.Content is { Length: > 0 } token)
                yield return token;

            if (chunk?.Done == true)
                yield break;
        }
    }

    /// <summary>
    /// Send an image to Qwen3-VL with a structured extraction prompt. Returns parsed JSON.
    /// </summary>
    public async Task<T> ExtractFromImageAsync<T>(
        string extractionPrompt,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a document analysis AI. Extract the requested information from the image. " +
                          "Return ONLY valid JSON with no markdown formatting, no code blocks, no explanation. " +
                          "If a field cannot be determined, use null.";

        var result = await ChatAsync(
            systemPrompt, extractionPrompt,
            images: [imageBytes],
            temperature: 0.0,
            cancellationToken: cancellationToken);

        var responseText = result.Message.Content.Trim();

        // Strip markdown code blocks if present
        if (responseText.StartsWith("```"))
        {
            var firstNewline = responseText.IndexOf('\n');
            var lastFence = responseText.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                responseText = responseText[(firstNewline + 1)..lastFence].Trim();
        }

        return JsonSerializer.Deserialize<T>(responseText, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse extraction response as " + typeof(T).Name);
    }

    /// <summary>
    /// Check if Ollama is reachable and the model is loaded.
    /// </summary>
    public async Task<(bool IsHealthy, string ModelName, long ModelSize)> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                $"{_settings.BaseUrl}/api/tags", JsonOptions, cancellationToken);

            var model = response?.Models.FirstOrDefault(
                m => m.Name.StartsWith(_settings.Model, StringComparison.OrdinalIgnoreCase));

            if (model is null)
                return (false, _settings.Model, 0);

            return (true, model.Name, model.Size);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama health check failed");
            return (false, _settings.Model, 0);
        }
    }
}
