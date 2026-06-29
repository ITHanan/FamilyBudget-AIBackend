using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class AuthAndSubscriptionsTests(FamilyBudgetApiFactory factory) : IClassFixture<FamilyBudgetApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task RegisterLoginAndManageSubscriptions()
    {
        var client = factory.CreateClient();
        var username = $"user{Guid.NewGuid():N}"[..12];
        var password = "StrongPassword123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            username,
            "Test",
            "User",
            $"{username}@example.com",
            password));

        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.StatusCode == HttpStatusCode.OK, registerBody);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registered);
        Assert.False(string.IsNullOrWhiteSpace(registered.Token));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registered.User.Username, password));
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, loginBody);
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loggedIn);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loggedIn.Token);
        var createResponse = await client.PostAsJsonAsync("/api/subscriptions", new SubscriptionRequest(
            "Spotify",
            119m,
            BillingFrequency.Monthly,
            new DateOnly(2026, 7, 15),
            "Music"));

        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Spotify", created.Name);

        var list = await client.GetFromJsonAsync<IReadOnlyList<SubscriptionDto>>("/api/subscriptions", JsonOptions);
        Assert.NotNull(list);
        var subscription = Assert.Single(list);
        Assert.Equal(created.Id, subscription.Id);
    }

    [Fact]
    public async Task InvalidRegisterRequestReturnsValidationProblem()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "ab",
            firstName = "",
            lastName = "",
            email = "not-an-email",
            password = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
