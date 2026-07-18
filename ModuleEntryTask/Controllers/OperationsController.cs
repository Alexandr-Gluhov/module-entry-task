using Microsoft.AspNetCore.Mvc;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Exceptions;
using ModuleEntryTask.Models;
using ModuleEntryTask.Services;

namespace ModuleEntryTask.Controllers;

[ApiController]
[Route("operations")]
public class OperationsController(OperationService operationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOperationRequest request)
    {
        if (!Enum.TryParse<Currency>(request.Currency, ignoreCase: true, out var currency))
            return BadRequest(new { error = $"Unsupported currency '{request.Currency}'." });

        try
        {
            var operation = await operationService.CreateAsync(
                request.OperationId,
                decimal.Parse(request.Amount),
                currency,
                request.Description);

            return StatusCode(201, OperationMapper.ToResponse(operation));
        }
        catch (ConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
