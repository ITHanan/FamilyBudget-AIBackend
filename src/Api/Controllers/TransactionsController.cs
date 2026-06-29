using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(
    ITransactionService transactionService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpPut("{id:int}/category")]
    public async Task<ActionResult<BankTransactionDto>> UpdateCategory(
        int id,
        UpdateTransactionCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await transactionService.UpdateCategoryAsync(currentUser.UserId, id, request, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TransactionSummaryDto>> Summary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetSummaryAsync(currentUser.UserId, from, to, cancellationToken));
    }

    [HttpGet("recurring-candidates")]
    public async Task<ActionResult<IReadOnlyList<RecurringPaymentCandidateDto>>> RecurringCandidates(CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetRecurringCandidatesAsync(currentUser.UserId, cancellationToken));
    }
}
