using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy.CreateEstablishment;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Tests.Tenancy;

public sealed class CreateEstablishmentCommandHandlerTests
{
    [Fact]
    public async Task Creates_establishment_in_authenticated_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeRepository();
        var handler = new CreateEstablishmentCommandHandler(
            new FakeTenantContext(tenantId),
            repository,
            new FixedTimeProvider());

        var id = await handler.HandleAsync(
            new CreateEstablishmentCommand("Pizzaria Centro", "PIZZARIA CENTRO"),
            CancellationToken.None);

        Assert.Equal(id, repository.Added!.Id);
        Assert.Equal(tenantId, repository.Added.TenantId);
        Assert.Equal("pizzaria-centro", repository.Added.Slug.Value);
    }

    [Fact]
    public async Task Rejects_duplicate_global_slug()
    {
        var repository = new FakeRepository { ExistingSlug = "pizzaria-centro" };
        var handler = new CreateEstablishmentCommandHandler(
            new FakeTenantContext(Guid.NewGuid()),
            repository,
            new FixedTimeProvider());

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new CreateEstablishmentCommand("Centro", "pizzaria-centro"),
            CancellationToken.None));
        Assert.Null(repository.Added);
    }

    [Fact]
    public void Validator_rejects_input_shape_before_handler()
    {
        var result = new CreateEstablishmentCommandValidator().Validate(
            new CreateEstablishmentCommand(string.Empty, string.Empty));

        Assert.False(result.IsValid);
    }

    private sealed class FakeRepository : IEstablishmentRepository
    {
        public string? ExistingSlug { get; init; }
        public Establishment? Added { get; private set; }

        public Task<bool> SlugExistsAsync(string normalizedSlug, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingSlug == normalizedSlug);

        public Task AddAsync(Establishment establishment, CancellationToken cancellationToken)
        {
            Added = establishment;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public bool HasTenant => true;
        public Guid TenantId => tenantId;
        public bool HasUser => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => UserId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    }
}
