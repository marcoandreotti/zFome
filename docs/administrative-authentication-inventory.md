# Inventário de autenticação administrativa

## Reuso confirmado

- `Email` permanece o value object de normalização e validação de e-mail.
- `IPasswordHasher` e `AspNetPasswordHasher` atendem usuários de Tenant e de plataforma.
- `AdministrativeUser`, seus papéis e associações continuam sendo a fonte de autorização tenant-scoped.
- `ICommandDispatcher`, `IQueryDispatcher`, FluentValidation, middleware global e ProblemDetails permanecem no fluxo HTTP.
- `zFomeDbContext`, projeto dedicado de migrations e PostgreSQL permanecem no caminho de escrita.
- `TimeProvider` permanece a fonte de tempo testável.

## Novos conceitos justificados

- código público único do Tenant para desambiguar e-mails repetidos;
- identidade global separada para não violar `ITenantScopedEntity`;
- desafio de MFA e sessão persistida para uso único, revogação e rotação;
- porta de entrega de código para isolar o fornecedor de e-mail;
- initializer de bootstrap para criar a primeira identidade global por secrets.

Não foi encontrada abstração existente que represente esses comportamentos.
