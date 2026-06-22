using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class AIConversationService(
    AppDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IAIConversationService
{
    public async Task<IReadOnlyList<ConversationListDto>> GetConversationsAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.AIConversations
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new ConversationListDto(x.Id, x.Title, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationDto> CreateConversationAsync(int userId, CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversation = new AIConversation
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim()
        };

        dbContext.AIConversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(conversation, []);
    }

    public async Task<ConversationDto?> GetConversationAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.AIConversations
            .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        return conversation is null ? null : ToDto(conversation, conversation.Messages);
    }

    public async Task<SendMessageResponse?> SendMessageAsync(int userId, int conversationId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Message content is required.");
        }

        var conversation = await dbContext.AIConversations
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        var userMessage = new AIMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Content.Trim()
        };

        var context = await BuildSubscriptionContextAsync(userId, cancellationToken);
        var assistantContent = await AskAssistantAsync(request.Content.Trim(), context, cancellationToken);

        var assistantMessage = new AIMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = assistantContent
        };

        conversation.UpdatedAt = DateTime.UtcNow;
        if (conversation.Title == "New chat")
        {
            conversation.Title = request.Content.Trim().Length > 60
                ? request.Content.Trim()[..60]
                : request.Content.Trim();
        }

        dbContext.AIMessages.AddRange(userMessage, assistantMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SendMessageResponse(ToMessageDto(userMessage), ToMessageDto(assistantMessage));
    }

    public async Task<bool> DeleteConversationAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.AIConversations
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        dbContext.AIConversations.Remove(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> BuildSubscriptionContextAsync(int userId, CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.Subscriptions
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RenewalDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthly = Math.Round(subscriptions.Sum(TotalsCalculator.MonthlyCost), 2);
        var yearly = Math.Round(subscriptions.Sum(TotalsCalculator.YearlyCost), 2);
        var upcoming = subscriptions
            .Where(x => TotalsCalculator.RenewsWithin(x.RenewalDate, today, 7))
            .Select(x => $"{x.Name} renews on {TotalsCalculator.NextRenewalOnOrAfter(x.RenewalDate, today):yyyy-MM-dd} ({x.Cost:C}, {x.BillingFrequency})");

        var lines = subscriptions.Select(x =>
            $"- {x.Name}: {x.Cost:C} {x.BillingFrequency}, category {x.Category}, next renewal date basis {x.RenewalDate:yyyy-MM-dd}");

        return $"""
            Calculated by backend:
            Monthly recurring total: {monthly:C}
            Yearly recurring total: {yearly:C}
            Upcoming renewals in next 7 days: {string.Join("; ", upcoming.DefaultIfEmpty("None"))}

            Subscriptions:
            {string.Join(Environment.NewLine, lines.DefaultIfEmpty("- None"))}
            """;
    }

    private async Task<string> AskAssistantAsync(string question, string subscriptionContext, CancellationToken cancellationToken)
    {
        var provider = configuration["AI:Provider"] ?? "OpenAI";

        return provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? await AskOllamaAsync(question, subscriptionContext, cancellationToken)
            : await AskOpenAIAsync(question, subscriptionContext, cancellationToken);
    }

    private async Task<string> AskOpenAIAsync(string question, string subscriptionContext, CancellationToken cancellationToken)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "OpenAI is not configured yet. Add your API key to user secrets or an environment variable named OpenAI__ApiKey.";
        }

        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var client = httpClientFactory.CreateClient("openai");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "You are FamilyBudget AI. Explain subscription spending clearly for busy parents. Use the backend-calculated totals as facts; do not recalculate them." },
                new { role = "user", content = $"Subscription context:\n{subscriptionContext}\n\nQuestion:\n{question}" }
            },
            temperature = 0.2
        };

        using var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"OpenAI request failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "No response was returned.";
    }

    private async Task<string> AskOllamaAsync(string question, string subscriptionContext, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = configuration["Ollama:Model"] ?? "llama3.2";
        var client = httpClientFactory.CreateClient("ollama");

        var payload = new
        {
            model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = "You are FamilyBudget AI. Explain subscription spending clearly for busy parents. Use the backend-calculated totals as facts; do not recalculate them." },
                new { role = "user", content = $"Subscription context:\n{subscriptionContext}\n\nQuestion:\n{question}" }
            }
        };

        try
        {
            using var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/chat", payload, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"Ollama request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Make sure Ollama is running and the '{model}' model is installed.";
            }

            using var document = JsonDocument.Parse(json);
            return document.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response was returned.";
        }
        catch (HttpRequestException)
        {
            return $"Could not connect to Ollama at {baseUrl}. Start Ollama and run: ollama pull {model}";
        }
    }

    private static ConversationDto ToDto(AIConversation conversation, IEnumerable<AIMessage> messages) => new(
        conversation.Id,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt,
        messages.OrderBy(x => x.CreatedAt).Select(ToMessageDto).ToList());

    private static MessageDto ToMessageDto(AIMessage message) => new(message.Id, message.Role, message.Content, message.CreatedAt);
}
