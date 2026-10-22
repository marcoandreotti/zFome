using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;

namespace ZFome.Application.Communications;

public sealed record UpsertNotificationTemplateCommand(Guid EstablishmentId, Guid? Id, string Purpose, NotificationChannel Channel,
    string Language, string Subject, string Body, string? ProviderTemplateName, bool RequiresConsent, bool IsActive) : ICommand<Guid>;

public sealed class UpsertNotificationTemplateValidator : AbstractValidator<UpsertNotificationTemplateCommand>
{
    public UpsertNotificationTemplateValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(100).Matches("^[a-z0-9][a-z0-9._-]*$");
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Language).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Subject).MaximumLength(250);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
        RuleFor(x => x.Body).Must((command, body) => HasSupportedOrderParameters(command.Purpose, body))
            .WithMessage("Os modelos de pedido só podem usar os campos de substituição compatíveis.");
        RuleFor(x => x.Subject).Must((command, subject) => HasSupportedOrderParameters(command.Purpose, subject))
            .WithMessage("Os modelos de pedido só podem usar os campos de substituição compatíveis.");
        RuleFor(x => x.ProviderTemplateName).NotEmpty().MaximumLength(200).When(x => x.Channel == NotificationChannel.WhatsApp && x.IsActive);
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel == NotificationChannel.Email && x.IsActive);
    }

    private static bool HasSupportedOrderParameters(string purpose, string? value) =>
        !CustomerOrderNotificationPurposes.All.Contains(purpose)
        || System.Text.RegularExpressions.Regex.Matches(value ?? string.Empty, "\\{\\{([a-zA-Z0-9_.-]{1,50})\\}\\}")
            .Select(match => match.Groups[1].Value)
            .All(CustomerOrderNotificationParameters.All.Contains);
}

public sealed class UpsertNotificationTemplateHandler(EstablishmentScopeResolver scopes, INotificationWriteRepository repository, TimeProvider clock)
    : ICommandHandler<UpsertNotificationTemplateCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertNotificationTemplateCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(command.EstablishmentId, cancellationToken);
        return await repository.UpsertTemplateAsync(scope, new(command.Id, command.Purpose, command.Channel, command.Language,
            command.Subject, command.Body, command.ProviderTemplateName, command.RequiresConsent, command.IsActive),
            clock.GetUtcNow(), cancellationToken);
    }
}

public sealed record SetNotificationConsentCommand(Guid EstablishmentId, NotificationChannel Channel, string Purpose, string Destination,
    bool IsGranted, string? Source) : ICommand;

public sealed class SetNotificationConsentValidator : AbstractValidator<SetNotificationConsentCommand>
{
    public SetNotificationConsentValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(100).Matches("^[a-z0-9][a-z0-9._-]*$");
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Destination).EmailAddress().When(x => x.Channel == NotificationChannel.Email);
        RuleFor(x => x.Destination).Matches("^\\+?[1-9][0-9]{7,14}$").When(x => x.Channel == NotificationChannel.WhatsApp);
        RuleFor(x => x.Source).Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Informe uma origem verificável para o consentimento.").MaximumLength(200);
    }
}

public sealed class SetNotificationConsentHandler(EstablishmentScopeResolver scopes, INotificationWriteRepository repository, TimeProvider clock)
    : ICommandHandler<SetNotificationConsentCommand>
{
    public async Task HandleAsync(SetNotificationConsentCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(command.EstablishmentId, cancellationToken);
        await repository.SetConsentAsync(scope, new(command.Channel, command.Purpose, command.Destination, command.IsGranted,
            clock.GetUtcNow(), command.Source), clock.GetUtcNow(), cancellationToken);
    }
}

public sealed record RequestNotificationCommand(Guid EstablishmentId, Guid TemplateId, NotificationChannel Channel, string Destination,
    IReadOnlyDictionary<string, string> Parameters, string IdempotencyKey) : ICommand<Guid>;

public sealed class RequestNotificationValidator : AbstractValidator<RequestNotificationCommand>
{
    public RequestNotificationValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Destination).EmailAddress().When(x => x.Channel == NotificationChannel.Email);
        RuleFor(x => x.Destination).Matches("^\\+?[1-9][0-9]{7,14}$").When(x => x.Channel == NotificationChannel.WhatsApp);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Parameters).NotNull().Must(x => x.Count <= 20 && x.All(p => !string.IsNullOrWhiteSpace(p.Key) && p.Key.Length <= 50 && p.Value is not null && p.Value.Length <= 1000));
    }
}

public sealed class RequestNotificationHandler(EstablishmentScopeResolver scopes, INotificationWriteRepository repository, TimeProvider clock)
    : ICommandHandler<RequestNotificationCommand, Guid>
{
    public async Task<Guid> HandleAsync(RequestNotificationCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(command.EstablishmentId, cancellationToken);
        var template = await repository.FindTemplateAsync(scope, command.TemplateId, cancellationToken)
            ?? throw new NotFoundException("Modelo de notificação não encontrado.");
        if (!template.IsActive) throw new ConflictException("Notification template is inactive.");
        if (template.Channel != command.Channel)
            throw new ConflictException("O canal de notificação não corresponde ao modelo selecionado.");

        var requiredParameters = System.Text.RegularExpressions.Regex.Matches(template.Body + "\n" + template.Subject,
                "\\{\\{([a-zA-Z0-9_.-]{1,50})\\}\\}")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if (!requiredParameters.SetEquals(command.Parameters.Keys))
            throw new FluentValidation.ValidationException("Os parâmetros da notificação devem corresponder aos campos do modelo selecionado.");
        if (command.Channel == NotificationChannel.WhatsApp)
        {
            var indexes = requiredParameters.Select(value => int.TryParse(value, out var index) ? index : -1).Order().ToArray();
            if (!indexes.SequenceEqual(Enumerable.Range(1, indexes.Length)))
                throw new FluentValidation.ValidationException("Os campos do modelo de WhatsApp devem ser numéricos e sequenciais, começando em {{1}}.");
        }

        var parametersJson = System.Text.Json.JsonSerializer.Serialize(command.Parameters.OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
        var result = await repository.RequestAsync(scope, new(command.EstablishmentId, command.TemplateId, command.Destination,
            command.Parameters, command.IdempotencyKey), template, parametersJson, clock.GetUtcNow(), cancellationToken);
        return result.Id;
    }
}

public sealed record ListNotificationTemplatesQuery(Guid EstablishmentId) : IQuery<IReadOnlyList<NotificationTemplateView>>;
public sealed class ListNotificationTemplatesHandler(EstablishmentScopeResolver scopes, INotificationReadGateway reads)
    : IQueryHandler<ListNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateView>>
{
    public async Task<IReadOnlyList<NotificationTemplateView>> HandleAsync(ListNotificationTemplatesQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await reads.ListTemplatesAsync(scope, cancellationToken);
    }
}

public sealed record ListNotificationConsentsQuery(Guid EstablishmentId) : IQuery<IReadOnlyList<NotificationConsentView>>;
public sealed class ListNotificationConsentsHandler(EstablishmentScopeResolver scopes, INotificationReadGateway reads)
    : IQueryHandler<ListNotificationConsentsQuery, IReadOnlyList<NotificationConsentView>>
{
    public async Task<IReadOnlyList<NotificationConsentView>> HandleAsync(ListNotificationConsentsQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await reads.ListConsentsAsync(scope, cancellationToken);
    }
}

public sealed record ListNotificationHistoryQuery(Guid EstablishmentId, int Page, int PageSize, DateTimeOffset? FromUtc, DateTimeOffset? ToUtcExclusive) : IQuery<IReadOnlyList<NotificationHistoryView>>;
public sealed class ListNotificationHistoryValidator : AbstractValidator<ListNotificationHistoryQuery>
{
    public ListNotificationHistoryValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.FromUtc is null || x.ToUtcExclusive is null || x.FromUtc < x.ToUtcExclusive)
            .WithMessage("A data inicial deve ser anterior à data final.");
    }
}
public sealed class ListNotificationHistoryHandler(EstablishmentScopeResolver scopes, INotificationReadGateway reads)
    : IQueryHandler<ListNotificationHistoryQuery, IReadOnlyList<NotificationHistoryView>>
{
    public async Task<IReadOnlyList<NotificationHistoryView>> HandleAsync(ListNotificationHistoryQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await reads.ListHistoryAsync(scope, query.Page, query.PageSize, query.FromUtc, query.ToUtcExclusive, cancellationToken);
    }
}

public sealed record ListNotificationAttemptsQuery(Guid EstablishmentId, Guid NotificationId) : IQuery<IReadOnlyList<NotificationAttemptView>>;
public sealed class ListNotificationAttemptsHandler(EstablishmentScopeResolver scopes, INotificationReadGateway reads)
    : IQueryHandler<ListNotificationAttemptsQuery, IReadOnlyList<NotificationAttemptView>>
{
    public async Task<IReadOnlyList<NotificationAttemptView>> HandleAsync(ListNotificationAttemptsQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await reads.ListAttemptsAsync(scope, query.NotificationId, cancellationToken);
    }
}
