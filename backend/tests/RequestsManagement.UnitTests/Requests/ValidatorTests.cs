using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Application.Requests.Validation;

namespace RequestsManagement.UnitTests.Requests;

public class ValidatorTests
{
    private static readonly string ValidVersion = RowVersions.Encode([0, 0, 0, 0, 0, 0, 7, 209]);

    [Fact]
    public void Default_query_is_valid() =>
        Assert.True(new RequestQueryValidator().Validate(new RequestQuery()).IsValid);

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    [InlineData(100_001, 20, "Page")]
    public void Out_of_range_paging_is_rejected(int page, int pageSize, string field)
    {
        var result = new RequestQueryValidator().Validate(new RequestQuery { Page = page, PageSize = pageSize });

        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Reversed_date_range_is_rejected()
    {
        var result = new RequestQueryValidator().Validate(new RequestQuery
        {
            CreatedFrom = new DateOnly(2026, 2, 1),
            CreatedTo = new DateOnly(2026, 1, 1),
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestQuery.CreatedTo));
    }

    [Fact]
    public void Unknown_enum_values_and_long_search_are_rejected()
    {
        var result = new RequestQueryValidator().Validate(new RequestQuery
        {
            Status = [(RequestStatus)99],
            Priority = [(RequestPriority)42],
            Search = new string('א', 101),
        });

        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("Status", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("Priority", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestQuery.Search));
    }

    [Fact]
    public void Search_with_control_characters_is_rejected()
    {
        var result = new RequestQueryValidator().Validate(new RequestQuery { Search = "היתר\u001Fאורן" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestQuery.Search));
    }

    [Fact]
    public void Valid_bulk_command_passes() =>
        Assert.True(new BulkUpdateStatusCommandValidator().Validate(Bulk([new(1, ValidVersion), new(2, ValidVersion)])).IsValid);

    [Fact]
    public void Bulk_without_items_is_rejected() =>
        Assert.False(new BulkUpdateStatusCommandValidator().Validate(Bulk([])).IsValid);

    [Fact]
    public void Bulk_with_more_than_100_items_is_rejected()
    {
        var items = Enumerable.Range(1, 101).Select(i => new BulkUpdateItem(i, ValidVersion)).ToList();

        Assert.False(new BulkUpdateStatusCommandValidator().Validate(Bulk(items)).IsValid);
    }

    [Fact]
    public void Bulk_with_duplicate_ids_is_rejected() =>
        Assert.False(new BulkUpdateStatusCommandValidator().Validate(Bulk([new(1, ValidVersion), new(1, ValidVersion)])).IsValid);

    [Fact]
    public void Bulk_with_an_invalid_version_is_rejected() =>
        Assert.False(new BulkUpdateStatusCommandValidator().Validate(Bulk([new(1, "not-a-version")])).IsValid);

    [Fact]
    public void Bulk_without_user_is_rejected() =>
        Assert.False(new BulkUpdateStatusCommandValidator().Validate(Bulk([new(1, ValidVersion)], changedBy: " ")).IsValid);

    private static BulkUpdateStatusCommand Bulk(IReadOnlyList<BulkUpdateItem> items, string changedBy = "דוד") =>
        new(new BulkUpdateStatusRequest { NewStatus = RequestStatus.Completed, Items = items }, changedBy);
}
