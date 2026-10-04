using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Identity.Application;
using Oism.Identity.Application.Users;
using Oism.Identity.Application.Users.CreateUser;
using Oism.Identity.Application.Users.ListUsers;
using Oism.Identity.Application.Users.UpdateUser;

namespace Oism.Identity.Api.Controllers;

public sealed record CreateUserRequest(string FullName, string? Email, string? Phone, string Password, string Role, Guid? BranchId);

public sealed record UpdateUserRequest(string FullName, string Role, Guid? BranchId, bool IsActive);

[ApiController]
[Route("users")]
public sealed class UsersController(ListUsersHandler listUsers, CreateUserHandler createUser, UpdateUserHandler updateUser)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await listUsers.Handle(new ListUsersQuery(page, pageSize), ct));

    [HttpPost]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await createUser.Handle(
            new CreateUserCommand(request.FullName, request.Email, request.Phone, request.Password, request.Role, request.BranchId), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        Ok(await updateUser.Handle(
            new UpdateUserCommand(id, request.FullName, request.Role, request.BranchId, request.IsActive), ct));
}
