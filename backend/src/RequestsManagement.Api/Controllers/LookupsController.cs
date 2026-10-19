using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Services;

namespace RequestsManagement.Api.Controllers;

/// <summary>Values for the filter drop-downs and the "current user" picker.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public sealed class LookupsController(RequestQueryService queries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LookupsDto>(StatusCodes.Status200OK)]
    public Task<LookupsDto> Get(CancellationToken cancellationToken) => queries.GetLookupsAsync(cancellationToken);
}
