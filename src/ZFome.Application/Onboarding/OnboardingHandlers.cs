using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity.Management;
using ZFome.Application.Tenancy;
using ZFome.Domain.Operations;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Onboarding;

public sealed class OnboardingQueries(EstablishmentScopeResolver scopes, ITenantContext context, IAdministrativeUserReadGateway users, IOnboardingReadGateway gateway) :
    IQueryHandler<GetOnboardingQuery, OnboardingProgress>, IQueryHandler<GetConfigurationQuery, ConfigurationReadModel>, IQueryHandler<SearchTablesQuery, TableSearchResult>
{
    private async Task<OperationalScope> AuthorizeAsync(Guid id, CancellationToken ct)
    {
        var scope = await scopes.ResolveAsync(id, ct);
        if (!await users.CanManageAsync(scope.TenantId, scope.UserId, context.IsPlatformUser, ct)) throw new ForbiddenException("Unit administration is not permitted.");
        return scope;
    }
    public async Task<OnboardingProgress> HandleAsync(GetOnboardingQuery query, CancellationToken ct) => await gateway.GetProgressAsync((await AuthorizeAsync(query.EstablishmentId, ct)).TenantId, query.EstablishmentId, ct);
    public async Task<ConfigurationReadModel> HandleAsync(GetConfigurationQuery query, CancellationToken ct) => await gateway.GetConfigurationAsync((await AuthorizeAsync(query.EstablishmentId, ct)).TenantId, query.EstablishmentId, ct);
    public async Task<TableSearchResult> HandleAsync(SearchTablesQuery query, CancellationToken ct) => await gateway.SearchTablesAsync((await AuthorizeAsync(query.EstablishmentId, ct)).TenantId, query, ct);
}

public sealed class UpdateEstablishmentHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository, TimeProvider time) : ICommandHandler<UpdateEstablishmentCommand>
{
    public Task HandleAsync(UpdateEstablishmentCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var unit = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, token);
        unit.Rename(command.TradeName, time.GetUtcNow());
        unit.ChangeSlug(new Slug(command.Slug), time.GetUtcNow());
        unit.ChangeTimeZone(command.TimeZoneId, time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}
public sealed class UpdateThemeHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository, TimeProvider time) : ICommandHandler<UpdateThemeCommand>
{
    public Task HandleAsync(UpdateThemeCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var unit = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, token);
        unit.ChangeTheme(command.Theme.ToDomain(), time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}
public sealed class ReplaceBusinessHoursHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository) : ICommandHandler<ReplaceBusinessHoursCommand>
{
    public Task HandleAsync(ReplaceBusinessHoursCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var replacement = command.Hours.Select(h => BusinessHours.Create(scope.TenantId, scope.EstablishmentId, h.DayOfWeek, h.OpensAt, h.ClosesAt)).ToArray();
        BusinessHours.EnsureNoOverlaps(replacement);
        var previous = await repository.GetHoursAsync(scope.TenantId, scope.EstablishmentId, token);
        repository.ReplaceHours(previous, replacement);
        await repository.SaveAsync(token);
    }, ct);
}
public sealed class CreateTableHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository) : ICommandHandler<CreateTableCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateTableCommand command, CancellationToken ct)
    {
        Guid result = default;
        await management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var existing = await repository.FindTableIntentAsync(scope.TenantId, scope.EstablishmentId, command.IntentId, token);
            if (existing is not null) { result = existing.Id; return; }
            var table = ServiceTable.Create(scope.TenantId, scope.EstablishmentId, command.Code, command.Description);
            table.IdentifyCreation(command.IntentId);
            repository.AddTable(table);
            await repository.SaveAsync(token);
            result = table.Id;
        }, ct);
        return result;
    }
}
public sealed class UpdateTableHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository) : ICommandHandler<UpdateTableCommand>, ICommandHandler<RotateTableTokenCommand>
{
    public Task HandleAsync(UpdateTableCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var table = await repository.GetTableAsync(scope.TenantId, scope.EstablishmentId, command.TableId, token) ?? throw new NotFoundException("Table was not found.");
        table.Update(command.Code, command.Description, command.IsActive);
        await repository.SaveAsync(token);
    }, ct);
    public Task HandleAsync(RotateTableTokenCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var table = await repository.GetTableAsync(scope.TenantId, scope.EstablishmentId, command.TableId, token) ?? throw new NotFoundException("Table was not found.");
        table.RevokeToken();
        await repository.SaveAsync(token);
    }, ct);
}
public sealed class CompleteOnboardingHandler(AdministrativeUserManagement management, IEstablishmentConfigurationRepository repository, TimeProvider time) : ICommandHandler<CompleteOnboardingCommand>
{
    public Task HandleAsync(CompleteOnboardingCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var unit = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, token);
        var hours = await repository.GetHoursAsync(scope.TenantId, scope.EstablishmentId, token);
        var administrators = await repository.CountAdministratorsAsync(scope.TenantId, scope.EstablishmentId, token);
        var progress = OnboardingProgress.Calculate(unit.TradeName, unit.Slug.Value, unit.IsActive, hours.Count(h => h.IsActive && h.ClosesAt != h.OpensAt), administrators, 0, unit.OnboardingCompletedAt);
        if (!progress.IsReady) throw new ConflictException("Complete the required steps: " + string.Join(", ", progress.PendingSteps));
        unit.RecordOnboardingCompletion(time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}
