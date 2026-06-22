using Domain.Enums;

namespace Application.DTOs;

public sealed record SubscriptionRequest(
    string Name,
    decimal Cost,
    BillingFrequency BillingFrequency,
    DateOnly RenewalDate,
    string Category);

public sealed record SubscriptionDto(
    int Id,
    string Name,
    decimal Cost,
    BillingFrequency BillingFrequency,
    DateOnly RenewalDate,
    string Category,
    DateTime CreatedAt);
