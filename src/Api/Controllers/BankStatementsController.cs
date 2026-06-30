using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bank-statements")]
public sealed class BankStatementsController(
    IBankStatementImportService importService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<BankStatementImportResultDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new { message = "Upload a PDF file." });
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(
            currentUser.UserId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BankStatementDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await importService.GetStatementsAsync(currentUser.UserId, cancellationToken));
    }

    [HttpGet("{id:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<BankTransactionDto>>> GetTransactions(int id, CancellationToken cancellationToken)
    {
        var transactions = await importService.GetTransactionsAsync(currentUser.UserId, id, cancellationToken);
        return transactions is null ? NotFound() : Ok(transactions);
    }
}
