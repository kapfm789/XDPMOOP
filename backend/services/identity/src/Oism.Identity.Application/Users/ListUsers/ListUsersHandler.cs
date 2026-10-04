using FluentValidation;

namespace Oism.Identity.Application.Users.ListUsers;

public sealed record ListUsersQuery(int Page = 1, int PageSize = 20);

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListUsersHandler(IUserRepository users)
{
    private static readonly ListUsersValidator Validator = new();

    public Task<PagedResult<UserDto>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return users.ListAsync(query.Page, query.PageSize, ct);
    }
}
