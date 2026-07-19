using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Exceptions;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class OperationService(ApplicationDbContext db)
{
    public async Task<List<OperationEventResponse>> GetEventsAsync(string operationId)
    {
        var exists = await db.Operations.AnyAsync(o => o.Id == operationId);
        if (!exists)
            throw new NotFoundException($"Operation '{operationId}' not found.");

        var rawEvents = await db.OperationEvents
            .Where(e => e.OperationId == operationId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        var events = rawEvents
            .Select((e, index) => new OperationEventResponse
            {
                EventId = index + 1,
                Type = e.Type.ToString().ToUpper(),
                FromStatus = e.FromStatus != null ? e.FromStatus.ToString()!.ToUpper() : null,
                ToStatus = e.ToStatus != null ? e.ToStatus.ToString()!.ToUpper() : null,
                Message = e.Message,
                OccurredAt = e.OccurredAt,
            })
            .ToList();

        return events;
    }

    public async Task<Operation> GetByIdAsync(string operationId)
    {
        return await db.Operations.FindAsync(operationId)
            ?? throw new NotFoundException($"Operation '{operationId}' not found.");
    }

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

    public async Task<(Operation operation, bool created)> SubmitAsync(string operationId)
    {
        var operation = await db.Operations.FindAsync(operationId)
            ?? throw new NotFoundException($"Operation '{operationId}' not found.");

        if (operation.Status != OperationStatus.Created)
            return (operation, false);

        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            operation.Status = OperationStatus.Processing;

            db.OperationEvents.Add(new OperationEvent
            {
                OperationId = operationId,
                Type = EventType.Processing,
                FromStatus = OperationStatus.Created,
                ToStatus = OperationStatus.Processing,
                Message = "Operation submitted for processing",
                OccurredAt = DateTime.UtcNow,
            });

            db.SubmitIntents.Add(new SubmitIntent
            {
                OperationId = operationId,
                AttemptCount = 0,
                RetryAfter = null,
            });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return (operation, true);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            var current = await db.Operations.FindAsync(operationId);
            return (current!, false);
        }
    }
}