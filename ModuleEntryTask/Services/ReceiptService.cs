using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Exceptions;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class ReceiptService(ApplicationDbContext db, ILogger<ReceiptService> logger)
{
    public async Task ProcessAsync(ReceiptRequest request)
    {
        if (!Enum.TryParse<OperationStatus>(request.Result, ignoreCase: true, out var incomingStatus)
            || incomingStatus is not (OperationStatus.Completed or OperationStatus.Rejected))
        {
            throw new ValidationException($"Invalid result value '{request.Result}'.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var operation = await db.Operations
            .Include(o => o.SubmitIntent)
            .FirstOrDefaultAsync(o => o.Id == request.OperationId)
            ?? throw new NotFoundException($"Operation '{request.OperationId}' not found.");

        if (operation.ProviderPaymentId != null
            && operation.ProviderPaymentId != request.ProviderPaymentId)
        {
            logger.LogWarning(
                "providerPaymentId mismatch. OperationId: {OperationId}, Expected: {Expected}, Got: {Got}",
                request.OperationId, operation.ProviderPaymentId, request.ProviderPaymentId);

            throw new ConflictException(
                $"providerPaymentId mismatch: expected '{operation.ProviderPaymentId}'.");
        }

        if (operation.Status is OperationStatus.Completed or OperationStatus.Rejected
            && operation.ProviderPaymentId == request.ProviderPaymentId)
        {
            if (operation.Status == incomingStatus)
            {
                logger.LogInformation(
                    "Duplicate receipt ignored. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Result: {Result}",
                    request.OperationId, request.ProviderPaymentId, request.Result);

                await transaction.RollbackAsync();
                return;
            }

            logger.LogWarning(
                "Late conflicting receipt ignored. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, CurrentStatus: {CurrentStatus}, IncomingResult: {IncomingResult}",
                request.OperationId, request.ProviderPaymentId, operation.Status, request.Result);

            db.OperationEvents.Add(new OperationEvent
            {
                OperationId = operation.Id,
                Type = EventType.Ignored,
                FromStatus = operation.Status,
                ToStatus = null,
                Message = $"Late conflicting receipt ignored: {request.Result}",
                OccurredAt = request.OccurredAt,
            });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return;
        }

        if (operation.Status is OperationStatus.Completed or OperationStatus.Rejected)
        {
            await transaction.RollbackAsync();
            return;
        }

        if (operation.ProviderPaymentId == null)
            operation.ProviderPaymentId = request.ProviderPaymentId;

        var fromStatus = operation.Status;
        operation.Status = incomingStatus;

        db.OperationEvents.Add(new OperationEvent
        {
            OperationId = operation.Id,
            Type = incomingStatus == OperationStatus.Completed
                ? EventType.Completed
                : EventType.Rejected,
            FromStatus = fromStatus,
            ToStatus = incomingStatus,
            Message = request.Message,
            OccurredAt = request.OccurredAt,
        });

        if (operation.SubmitIntent != null)
            db.SubmitIntents.Remove(operation.SubmitIntent);
        else
        {
            var intent = await db.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == operation.Id);
            if (intent != null)
                db.SubmitIntents.Remove(intent);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Receipt processed. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Result: {Result}",
            request.OperationId, request.ProviderPaymentId, request.Result);
    }
}
