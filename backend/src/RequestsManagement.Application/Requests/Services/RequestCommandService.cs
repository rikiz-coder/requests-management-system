using FluentValidation;
using Microsoft.Extensions.Logging;
using RequestsManagement.Application.Requests.Abstractions;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Application.Requests.Exceptions;

namespace RequestsManagement.Application.Requests.Services;

/// <summary>Write side: status changes, single and bulk, always guarded by the row version.</summary>
public sealed class RequestCommandService(
    IRequestRepository repository,
    ISummaryCache summaryCache,
    IValidator<UpdateStatusCommand> updateValidator,
    IValidator<BulkUpdateStatusCommand> bulkValidator,
    TimeProvider timeProvider,
    ILogger<RequestCommandService> logger)
{
    public async Task<RequestDetailsDto> UpdateStatusAsync(UpdateStatusCommand command, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(command, cancellationToken);

        var request = await repository.GetForUpdateAsync(command.RequestId, cancellationToken)
            ?? throw new NotFoundException(command.RequestId);

        // The version the client saw is already outdated: no need to go further.
        // (This is only a shortcut – the database enforces the same check inside the UPDATE.)
        if (!request.RowVersion.AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw await ConflictAsync(command.RequestId, cancellationToken);
        }

        var previousStatus = request.Status;
        request.ChangeStatus(command.NewStatus, command.ChangedBy.Trim(), Now());

        try
        {
            await repository.SaveStatusChangeAsync(request, command.ExpectedVersion, cancellationToken);
        }
        catch (StaleVersionException)
        {
            throw await ConflictAsync(command.RequestId, cancellationToken);
        }

        summaryCache.Invalidate();
        logger.LogInformation("Request {RequestId} status changed from {PreviousStatus} to {NewStatus}",
            request.Id, previousStatus, request.Status);

        return request.ToDetailsDto();
    }

    /// <summary>
    /// Updates up to 100 requests. Partial success: each item is checked on its own (not found, outdated
    /// version, transition not allowed); all valid items are saved together, and every item gets an outcome.
    /// </summary>
    public async Task<BulkUpdateStatusResult> BulkUpdateStatusAsync(BulkUpdateStatusCommand command, CancellationToken cancellationToken)
    {
        await bulkValidator.ValidateAndThrowAsync(command, cancellationToken);

        var items = command.Request.Items!;
        var newStatus = command.Request.NewStatus;
        var changedBy = command.ChangedBy.Trim();
        var now = Now();

        var loaded = (await repository.GetManyForUpdateAsync(items.Select(i => i.Id).ToArray(), cancellationToken))
            .ToDictionary(r => r.Id);

        var outcomes = new Dictionary<int, BulkItemResult>();
        var changes = new List<(ServiceRequest Request, byte[] ExpectedVersion)>();

        foreach (var item in items)
        {
            RowVersions.TryDecode(item.Version, out var expectedVersion); // already validated

            if (!loaded.TryGetValue(item.Id, out var request))
            {
                outcomes[item.Id] = Failed(item.Id, BulkItemOutcome.NotFound, "The request was not found.");
            }
            else if (!request.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            {
                outcomes[item.Id] = Failed(item.Id, BulkItemOutcome.Conflict, "The request was modified by another user.");
            }
            else if (!StatusTransitions.IsAllowed(request.Status, newStatus))
            {
                outcomes[item.Id] = Failed(item.Id, BulkItemOutcome.InvalidTransition,
                    $"Cannot move from {request.Status} to {newStatus}.");
            }
            else
            {
                request.ChangeStatus(newStatus, changedBy, now);
                changes.Add((request, expectedVersion));
            }
        }

        if (changes.Count > 0)
        {
            var conflicted = await repository.SaveStatusChangesAsync(changes, cancellationToken);
            foreach (var (request, _) in changes)
            {
                outcomes[request.Id] = conflicted.Contains(request.Id)
                    ? Failed(request.Id, BulkItemOutcome.Conflict, "The request was modified by another user.")
                    : new BulkItemResult(request.Id, BulkItemOutcome.Updated, null, RowVersions.Encode(request.RowVersion));
            }

            if (conflicted.Count < changes.Count)
            {
                summaryCache.Invalidate();
            }
        }

        var results = items.Select(i => outcomes[i.Id]).ToList();
        var succeeded = results.Count(r => r.Outcome == BulkItemOutcome.Updated);
        logger.LogInformation("Bulk status change to {NewStatus}: {Succeeded} updated, {Failed} not updated",
            newStatus, succeeded, results.Count - succeeded);

        return new BulkUpdateStatusResult(results.Count, succeeded, results.Count - succeeded, results);
    }

    private async Task<ConcurrencyConflictException> ConflictAsync(int requestId, CancellationToken cancellationToken)
    {
        var current = await repository.GetAsync(requestId, cancellationToken);
        var lastChange = await repository.GetLastChangeAsync(requestId, cancellationToken);
        return new ConcurrencyConflictException(requestId, current?.ToDetailsDto(), lastChange);
    }

    private static BulkItemResult Failed(int id, BulkItemOutcome outcome, string error) => new(id, outcome, error, null);

    /// <summary>Current UTC time at the column's precision (milliseconds), so the value returned equals the value stored.</summary>
    private DateTime Now()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
    }
}
