using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs;

public sealed record SubscriptionRequest(
    [Required, MaxLength(160)]
    string Name,
    [Range(0, 999999999)]
    decimal Cost,
    BillingFrequency BillingFrequency,
    DateOnly RenewalDate,
    [Required, MaxLength(100)]
    string Category);

public sealed record SubscriptionDto(
    int Id,
    string Name,
    decimal Cost,
    BillingFrequency BillingFrequency,
    DateOnly RenewalDate,
    string Category,
    DateTime CreatedAt);
