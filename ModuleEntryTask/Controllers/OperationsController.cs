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
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var operation = await operationService.GetByIdAsync(id);
            return Ok(OperationMapper.ToResponse(operation));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

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

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(string id)
    {
        try
        {
            var (operation, created) = await operationService.SubmitAsync(id);
            return created
                ? StatusCode(202, OperationMapper.ToResponse(operation))
                : Ok(OperationMapper.ToResponse(operation));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
