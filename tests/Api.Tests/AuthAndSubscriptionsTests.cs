using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.DTOs;
using Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class AuthAndSubscriptionsTests(FamilyBudgetApiFactory factory) : IClassFixture<FamilyBudgetApiFactory>
{
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

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registered);
        Assert.False(string.IsNullOrWhiteSpace(registered.Token));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loggedIn);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loggedIn.Token);
        var createResponse = await client.PostAsJsonAsync("/api/subscriptions", new SubscriptionRequest(
            "Spotify",
            119m,
            BillingFrequency.Monthly,
            new DateOnly(2026, 7, 15),
            "Music"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(created);
        Assert.Equal("Spotify", created.Name);

        var list = await client.GetFromJsonAsync<IReadOnlyList<SubscriptionDto>>("/api/subscriptions");
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
