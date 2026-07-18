using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.Exceptions;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class OperationService(ApplicationDbContext db)
{
    public async Task<Operation> CreateAsync(
        string operationId,
        decimal amount,
        Currency currency,
        string description)
    {
        var exists = await db.Operations.AnyAsync(o => o.Id == operationId);
        if (exists)
            throw new ConflictException($"Operation '{operationId}' already exists.");

        var operation = new Operation
        {
            Id = operationId,
            Amount = amount,
            Currency = currency,
            Description = description,
            Status = OperationStatus.Created,
        };

        var operationEvent = new OperationEvent
        {
            OperationId = operationId,
            Type = EventType.Created,
            FromStatus = null,
            ToStatus = OperationStatus.Created,
            Message = "Operation created",
            OccurredAt = DateTime.UtcNow,
        };

        db.Operations.Add(operation);
        db.OperationEvents.Add(operationEvent);
        await db.SaveChangesAsync();

        return operation;
    }
}