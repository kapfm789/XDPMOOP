using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Channel.Application.Shops;
using Oism.Channel.Application.Shops.CreateChannelShop;
using Oism.Channel.Application.Shops.ListChannelShops;
using Oism.Channel.Application.Shops.SetChannelShopActive;

namespace Oism.Channel.Api.Controllers;

public sealed record CreateChannelShopRequest(string? Channel, string? ShopId, Guid BranchId);

public sealed record SetChannelShopActiveRequest(bool IsActive);

// UC-SIM-01: cấu hình shop của sàn, chỉ Owner (docs/architecture/security.md).
[ApiController]
[Route("channel-shops")]
public sealed class ChannelShopsController(
    ListChannelShopsHandler listShops,
    CreateChannelShopHandler createShop,
    SetChannelShopActiveHandler setActive) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<IReadOnlyList<ChannelShopDto>>> List(CancellationToken ct) =>
        Ok(await listShops.Handle(ct));

    // Phản hồi mang `secret`; các lần đọc sau không trả lại nó.
    [HttpPost]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<ChannelShopDto>> Create(CreateChannelShopRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await createShop.Handle(
            new CreateChannelShopCommand(request.Channel, request.ShopId, request.BranchId), ct));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<ChannelShopDto>> SetActive(Guid id, SetChannelShopActiveRequest request, CancellationToken ct) =>
        Ok(await setActive.Handle(new SetChannelShopActiveCommand(id, request.IsActive), ct));
}
