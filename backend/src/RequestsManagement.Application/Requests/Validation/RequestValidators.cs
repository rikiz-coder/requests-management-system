using FluentValidation;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Validation;

/// <summary>Rules for the filters, shared by the list and the summary.</summary>
public class RequestFilterValidator<T> : AbstractValidator<T>
    where T : RequestFilter
{
    public const int MaxSearchLength = 100;

    public RequestFilterValidator()
    {
        RuleForEach(f => f.Status).IsInEnum().WithMessage("Unknown status '{PropertyValue}'.");
        RuleForEach(f => f.Priority).IsInEnum().WithMessage("Unknown priority '{PropertyValue}'.");
        RuleFor(f => f.OrganizationName).MaximumLength(RequestFieldLimits.OrganizationNameMaxLength);
        RuleFor(f => f.AssignedTo).MaximumLength(RequestFieldLimits.PersonNameMaxLength);
        RuleFor(f => f.Search)
            .MaximumLength(MaxSearchLength)
            .Must(s => s is null || !s.Any(char.IsControl))
            .WithMessage("'search' must not contain control characters.");
        RuleFor(f => f.CreatedTo)
            .GreaterThanOrEqualTo(f => f.CreatedFrom)
            .When(f => f.CreatedFrom is not null && f.CreatedTo is not null)
            .WithMessage("'createdTo' must be on or after 'createdFrom'.");
    }
}

public sealed class RequestFilterValidator : RequestFilterValidator<RequestFilter>;

public sealed class RequestQueryValidator : RequestFilterValidator<RequestQuery>
{
    public RequestQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, RequestQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, RequestQuery.MaxPageSize);
        RuleFor(q => q.SortBy).IsInEnum();
        RuleFor(q => q.SortDirection).IsInEnum();
    }
}

public sealed class UpdateStatusCommandValidator : AbstractValidator<UpdateStatusCommand>
{
    public UpdateStatusCommandValidator()
    {
        RuleFor(c => c.NewStatus).IsInEnum().WithMessage("Unknown status '{PropertyValue}'.");
        RuleFor(c => c.ChangedBy).NotEmpty().MaximumLength(RequestFieldLimits.PersonNameMaxLength)
            .OverridePropertyName("X-User-Name");
        RuleFor(c => c.ExpectedVersion).Must(v => v.Length == RowVersions.Length);
    }
}

public sealed class BulkUpdateStatusCommandValidator : AbstractValidator<BulkUpdateStatusCommand>
{
    public const int MaxItems = 100;

    public BulkUpdateStatusCommandValidator()
    {
        RuleFor(c => c.ChangedBy).NotEmpty().MaximumLength(RequestFieldLimits.PersonNameMaxLength)
            .OverridePropertyName("X-User-Name");
        RuleFor(c => c.Request.NewStatus).IsInEnum().WithMessage("Unknown status '{PropertyValue}'.")
            .OverridePropertyName("newStatus");

        RuleFor(c => c.Request.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("At least one item is required.")
            .Must(items => items!.Count <= MaxItems).WithMessage($"At most {MaxItems} items per request.")
            .Must(items => items!.Select(i => i?.Id).Distinct().Count() == items!.Count)
                .WithMessage("Each request may appear only once.")
            .OverridePropertyName("items");

        RuleForEach(c => c.Request.Items)
            .NotNull().WithMessage("Item must not be null.")
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Id).GreaterThan(0);
                item.RuleFor(i => i.Version).Must(v => RowVersions.TryDecode(v, out _))
                    .WithMessage("'version' must be the value returned by the API.");
            })
            .OverridePropertyName("items");
    }
}
