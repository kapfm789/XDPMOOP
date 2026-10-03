using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.SharedKernel;
using Testcontainers.PostgreSql;

namespace Oism.BuildingBlocks.IntegrationTests.Tenancy;

public sealed class Note(string text) : ITenantOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid TenantId { get; private set; }

    public string Text { get; private set; } = text;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, "test")
{
    public DbSet<Note> Notes => Set<Note>();
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(tenantId: null);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public TestDbContext CreateContext(Guid? tenantId)
    {
        var tenant = new TenantContext();
        if (tenantId is { } id)
            tenant.Set(id);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TestDbContext(options, tenant);
    }
}

// Điều kiện xong của W1-02 (NFR-TENANT-01): entity có TenantId tự bị lọc.
public sealed class TenantQueryFilterTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    [Fact]
    public async Task Query_RowsOfTwoTenants_ReturnsOnlyCurrentTenant()
    {
        await SeedAsync(_tenantA, "a");
        await SeedAsync(_tenantB, "b");

        await using var db = postgres.CreateContext(_tenantA);
        var note = Assert.Single(await db.Notes.ToListAsync());

        Assert.Equal("a", note.Text);
        Assert.Equal(_tenantA, note.TenantId);
    }

    [Fact]
    public async Task Query_ByIdOfAnotherTenant_FindsNothing()
    {
        var id = await SeedAsync(_tenantB, "b");

        await using var db = postgres.CreateContext(_tenantA);

        Assert.Null(await db.Notes.SingleOrDefaultAsync(note => note.Id == id));
        Assert.Null(await db.Notes.FindAsync(id));
    }

    [Fact]
    public async Task Query_NoTenantContext_ReturnsNothing()
    {
        await SeedAsync(_tenantA, "a");

        await using var db = postgres.CreateContext(tenantId: null);

        Assert.Empty(await db.Notes.ToListAsync());
    }

    [Fact]
    public async Task SaveChanges_EntityOfAnotherTenant_Throws()
    {
        var note = new Note("a");
        await using (var dbA = postgres.CreateContext(_tenantA))
        {
            dbA.Add(note);
            await dbA.SaveChangesAsync();
        }

        await using var dbB = postgres.CreateContext(_tenantB);
        dbB.Update(note);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_NoTenantContext_Throws()
    {
        await using var db = postgres.CreateContext(tenantId: null);
        db.Add(new Note("x"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private async Task<Guid> SeedAsync(Guid tenantId, string text)
    {
        await using var db = postgres.CreateContext(tenantId);
        var note = new Note(text);
        db.Add(note);
        await db.SaveChangesAsync();
        return note.Id;
    }
}
