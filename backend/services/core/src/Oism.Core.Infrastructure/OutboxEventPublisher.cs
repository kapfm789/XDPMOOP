using Oism.BuildingBlocks.Messaging;
using Oism.Core.Application;

namespace Oism.Core.Infrastructure;

// Thông điệp nằm trong DbContext của use case nên được lưu cùng transaction với thay đổi nghiệp vụ.
internal sealed class OutboxEventPublisher(CoreDbContext db, IClock clock) : IEventPublisher
{
    public void Enqueue<TPayload>(TPayload payload)
        where TPayload : notnull => db.Add(OutboxMessage.Create(payload, clock.UtcNow));
}
