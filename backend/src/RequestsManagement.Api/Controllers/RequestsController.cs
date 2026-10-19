using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Api.Http;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Services;

namespace RequestsManagement.Api.Controllers;

/// <summary>
/// Requests: search, details, history, summary and status changes.
/// HTTP concerns only – the logic lives in the Application services.
/// </summary>
[ApiController]
[Route("api/requests")]
[Produces("application/json")]
public sealed class RequestsController(RequestQueryService queries, RequestCommandService commands) : ControllerBase
{
    public const string UserHeader = "X-User-Name";

    /// <summary>List of requests: filtering, search, sorting and paging – all in the database.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<RequestListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<RequestListItemDto>> Search([FromQuery] RequestQuery query, CancellationToken cancellationToken) =>
        queries.SearchAsync(query, cancellationToken);

    /// <summary>Aggregations (count by status and by priority) for the same filters as the list.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<RequestSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<RequestSummaryDto> Summary([FromQuery] RequestFilter filter, CancellationToken cancellationToken) =>
        queries.GetSummaryAsync(filter, cancellationToken);

    /// <summary>One request, with the statuses it may move to. The <c>ETag</c> header holds its version.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<RequestDetailsDto> Get(int id, CancellationToken cancellationToken)
    {
        var request = await queries.GetAsync(id, cancellationToken);
        Response.Headers.ETag = EntityTags.Format(request.Version);
        return request;
    }

    /// <summary>Status change history of a request, newest first.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<StatusHistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<StatusHistoryEntryDto>> History(int id, CancellationToken cancellationToken) =>
        queries.GetHistoryAsync(id, cancellationToken);

    /// <summary>
    /// Changes the status. <c>If-Match</c> must hold the <c>ETag</c> from GET: if the request changed since,
    /// the answer is 409 with its current state and who changed it.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Consumes("application/json")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateStatusRequest body,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = UserHeader)] string? userName,
        CancellationToken cancellationToken)
    {
        var precondition = EntityTags.ParseIfMatch(ifMatch);
        if (precondition.Status == IfMatchStatus.Missing)
        {
            return Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match header is required",
                detail: "Send the ETag returned by GET /api/requests/{id} in the If-Match header.");
        }

        if (precondition.Status == IfMatchStatus.Invalid)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid If-Match header",
                detail: "If-Match must hold a single ETag returned by the API. '*' and weak tags are not accepted.");
        }

        var updated = await commands.UpdateStatusAsync(
            new UpdateStatusCommand(id, body.NewStatus, precondition.Version, userName ?? string.Empty),
            cancellationToken);

        Response.Headers.ETag = EntityTags.Format(updated.Version);
        return Ok(updated);
    }

    /// <summary>
    /// Changes the status of up to 100 requests. Partial success: always 200 with an outcome per item
    /// (Updated, NotFound, Conflict, InvalidTransition); 400 only when the request itself is invalid.
    /// </summary>
    [HttpPost("bulk/status")]
    [Consumes("application/json")]
    [ProducesResponseType<BulkUpdateStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<BulkUpdateStatusResult> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusRequest body,
        [FromHeader(Name = UserHeader)] string? userName,
        CancellationToken cancellationToken) =>
        commands.BulkUpdateStatusAsync(new BulkUpdateStatusCommand(body, userName ?? string.Empty), cancellationToken);
}
