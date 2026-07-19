using Prometheus;

namespace ModuleEntryTask.Services;

public class PaymentMetrics
{
    private readonly Gauge _pendingOperations = Metrics.CreateGauge(
        "payment_pending_operations",
        "Number of operations with active submit intents.");

    private readonly Counter _retryAttempts = Metrics.CreateCounter(
        "payment_retry_attempts_total",
        "Total number of retry attempts to submit payment to provider.");

    private readonly Counter _submittedPayments = Metrics.CreateCounter(
        "payment_submitted_total",
        "Total number of payments successfully submitted to provider.");

    private readonly Counter _failedPayments = Metrics.CreateCounter(
        "payment_failed_total",
        "Total number of permanent failures submitting payment to provider.");

    public void SetPendingOperations(int count) => _pendingOperations.Set(count);
    public void IncrementRetryAttempts() => _retryAttempts.Inc();
    public void IncrementSubmitted() => _submittedPayments.Inc();
    public void IncrementFailed() => _failedPayments.Inc();
}
