using Microsoft.AspNetCore.Mvc;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Exceptions;
using ModuleEntryTask.Services;

namespace ModuleEntryTask.Controllers;

[ApiController]
[Route("receipts")]
public class ReceiptsController(ReceiptService receiptService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(ReceiptRequest request)
    {
        try
        {
            await receiptService.ProcessAsync(request);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
