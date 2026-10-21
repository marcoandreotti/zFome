using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Catalog;
using ZFome.Domain.SharedKernel;

namespace ZFome.Application.Catalog;

public sealed class UpsertCategoryCommandHandler(EstablishmentScopeResolver scopeResolver, ICategoryRepository repository, ICategoryHierarchyGateway hierarchyGateway) : ICommandHandler<UpsertCategoryCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertCategoryCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var category = command.Id is { } id
            ? await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Category was not found.")
            : Category.Create(scope.TenantId, scope.EstablishmentId, command.Name, command.Order);
        category.Update(command.Name, command.Description, command.Order, command.ImageUrl);
        if (command.ParentId is { } parentId)
        {
            var parent = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, parentId, cancellationToken) ?? throw new NotFoundException("Parent category was not found.");
            var ancestors = await hierarchyGateway.GetAncestorIdsAsync(scope.TenantId, scope.EstablishmentId, parentId, cancellationToken);
            category.ChangeParent(parent.Id, parent.TenantId, parent.EstablishmentId, ancestors);
        }
        else category.ChangeParent(null, Guid.Empty, Guid.Empty, new HashSet<Guid>());
        if (command.IsActive) category.Activate(); else category.Deactivate();
        if (command.Id is null) await repository.AddAsync(category, cancellationToken); else await repository.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}

public sealed class UpsertProductCommandHandler(EstablishmentScopeResolver scopeResolver, ICategoryRepository categoryRepository, IProductRepository repository, IAdditionalGroupRepository groupRepository) : ICommandHandler<UpsertProductCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertProductCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var category = await categoryRepository.GetAsync(scope.TenantId, scope.EstablishmentId, command.CategoryId, cancellationToken) ?? throw new NotFoundException("Category was not found.");
        var code = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(scope.TenantId, scope.EstablishmentId, code, command.Id, cancellationToken)) throw new ConflictException("Product code is already in use in this establishment.");
        var product = command.Id is { } id
            ? await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Product was not found.")
            : Product.Create(scope.TenantId, scope.EstablishmentId, category, code, command.Name, new Money(command.BasePrice));
        product.Update(category, code, command.Name, command.Description, new Money(command.BasePrice), command.IsFeatured, command.AllowsNotes);
        product.ReplaceImages(command.Images.Select(x => (x.Url, x.Order, x.IsPrincipal)));
        product.ReplaceVariations(command.Variations.Select(x => (x.Name, new Money(x.Price), x.Order, x.IsActive)));
        var groupIds = command.AdditionalGroups.Select(x => x.GroupId).Distinct().ToArray();
        var groups = await groupRepository.GetManyAsync(scope.TenantId, scope.EstablishmentId, groupIds, cancellationToken);
        if (groups.Count != groupIds.Length) throw new NotFoundException("One or more additional groups were not found.");
        var groupById = groups.ToDictionary(x => x.Id);
        product.ReplaceAdditionalGroups(command.AdditionalGroups.Select(x => (groupById[x.GroupId], x.Order)));
        if (command.IsActive) product.Activate(); else product.Deactivate();
        if (command.Id is null) await repository.AddAsync(product, cancellationToken); else await repository.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}

public sealed class UpsertAdditionalCommandHandler(EstablishmentScopeResolver scopeResolver, IAdditionalRepository repository) : ICommandHandler<UpsertAdditionalCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertAdditionalCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var additional = command.Id is { } id
            ? await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Additional was not found.")
            : Additional.Create(scope.TenantId, scope.EstablishmentId, command.Name, new Money(command.Price));
        additional.Update(command.Name, new Money(command.Price));
        if (command.IsActive) additional.Activate(); else additional.Deactivate();
        if (command.Id is null) await repository.AddAsync(additional, cancellationToken); else await repository.SaveChangesAsync(cancellationToken);
        return additional.Id;
    }
}

public sealed class UpsertAdditionalGroupCommandHandler(EstablishmentScopeResolver scopeResolver, IAdditionalGroupRepository repository, IAdditionalRepository additionalRepository) : ICommandHandler<UpsertAdditionalGroupCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertAdditionalGroupCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var group = command.Id is { } id
            ? await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Additional group was not found.")
            : AdditionalGroup.Create(scope.TenantId, scope.EstablishmentId, command.Name, command.MinimumSelection, command.MaximumSelection, command.PricingStrategy, command.Type, command.RequiresCompleteComposition);
        group.Update(command.Name, command.MinimumSelection, command.MaximumSelection, command.PricingStrategy, command.Type, command.RequiresCompleteComposition);
        var ids = command.Items.Select(x => x.AdditionalId).Distinct().ToArray();
        var additionals = await additionalRepository.GetManyAsync(scope.TenantId, scope.EstablishmentId, ids, cancellationToken);
        if (additionals.Count != ids.Length) throw new NotFoundException("One or more additionals were not found.");
        var byId = additionals.ToDictionary(x => x.Id);
        group.ReplaceItems(command.Items.Select(x => (byId[x.AdditionalId], x.Order)));
        var compatibilityInputs = command.Items.SelectMany(item => (item.CompatibilityRules ?? [])
            .Select(rule => new ModifierCompatibilityInput(item.AdditionalId, rule.TargetGroupId, rule.TargetAdditionalId, rule.Kind))).ToArray();
        var targetGroupIds = compatibilityInputs.Select(x => x.TargetGroupId).Distinct().Where(x => x != group.Id).ToArray();
        var targetGroups = await repository.GetManyAsync(scope.TenantId, scope.EstablishmentId, targetGroupIds, cancellationToken);
        if (targetGroups.Count != targetGroupIds.Length) throw new NotFoundException("One or more modifier compatibility groups were not found.");
        var groupsById = targetGroups.ToDictionary(x => x.Id);
        groupsById[group.Id] = group;
        if (compatibilityInputs.Any(rule => !groupsById.TryGetValue(rule.TargetGroupId, out var targetGroup)
            || targetGroup.Items.All(item => item.AdditionalId != rule.TargetAdditionalId)))
            throw new NotFoundException("One or more modifier compatibility options were not found in their groups.");
        group.ReplaceCompatibilityRules(compatibilityInputs);
        if (command.IsActive) group.Activate(); else group.Deactivate();
        if (command.Id is null) await repository.AddAsync(group, cancellationToken); else await repository.SaveChangesAsync(cancellationToken);
        return group.Id;
    }
}
