using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SubscriptionsController(ISubscriptionService subscriptionService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubscriptionDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await subscriptionService.GetAllAsync(currentUser.UserId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SubscriptionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(currentUser.UserId, id, cancellationToken);
        return subscription is null ? NotFound() : Ok(subscription);
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionDto>> Create(SubscriptionRequest request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = subscription.Id }, subscription);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SubscriptionDto>> Update(int id, SubscriptionRequest request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.UpdateAsync(currentUser.UserId, id, request, cancellationToken);
        return subscription is null ? NotFound() : Ok(subscription);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await subscriptionService.DeleteAsync(currentUser.UserId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
