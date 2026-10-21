# Migrations

As migrations de escrita pertencem ao `ZFomeDbContext`, mas ficam no projeto isolado `ZFome.Infrastructure.Migrations`.

Convenções:

- tabelas de módulo usam os schemas definidos em `DatabaseSchemas`;
- entidades pertencentes a estabelecimento incluem `tenant_id` e índice iniciado por esse campo;
- unicidades tenant-scoped incluem `tenant_id`;
- nenhuma migration de outro módulo deve alterar tabelas sem uma decisão explícita;
- migrations destrutivas exigem estratégia de implantação e rollback documentada.

## Comandos

Defina `<connection-string>` com um PostgreSQL de desenvolvimento, sem versionar credenciais reais.

Criar uma migration:

```powershell
dotnet ef migrations add <MigrationName> --project src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj --startup-project src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj --context ZFomeDbContext --output-dir Migrations -- --connection "<connection-string>"
```

Aplicar todas as migrations:

```powershell
dotnet run --project src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj -- --connection "<connection-string>"
```

Reverter todas as migrations em ambiente descartável:

```powershell
dotnet ef database update 0 --project src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj --startup-project src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj --context ZFomeDbContext -- --connection "<connection-string>"
```

Para reverter somente a migration `ProductCatalog`, use a migration anterior
`20260820120132_IdentityOperations` como destino. Esse downgrade remove todas as
tabelas do schema `catalog` e destrói os dados nelas armazenados; execute-o somente
em ambiente controlado ou depois de backup/exportação explícita.
