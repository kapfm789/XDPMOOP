using Oism.BuildingBlocks.Messaging;
using Oism.Catalog.Application;

namespace Oism.Catalog.Infrastructure;

// Thông điệp nằm trong DbContext của use case nên được lưu cùng transaction với thay đổi nghiệp vụ.
internal sealed class OutboxEventPublisher(CatalogDbContext db, TimeProvider time) : IEventPublisher
{
    public void Enqueue<TPayload>(TPayload payload)
        where TPayload : notnull => db.Add(OutboxMessage.Create(payload, time.GetUtcNow()));
}
