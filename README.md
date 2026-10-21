# zFome

Documentação operacional: [notificações de pedidos em tempo real](docs/realtime-order-notifications.md).

Fundação do monólito modular SaaS Multi-Tenant para gestão de pedidos de estabelecimentos de alimentação.

## Pré-requisitos

- .NET SDK 10
- Node.js 22.22 ou superior
- Docker 29 com Docker Compose

## Backend

```powershell
dotnet restore ZFome.sln
dotnet build ZFome.sln --no-restore
dotnet test ZFome.sln --no-build
dotnet run --project src/ZFome.Api
```

A API expõe liveness em `GET /health` e readiness do PostgreSQL em `GET /health/ready`.

## Frontend

```powershell
cd web/ZFome.Web
npm install
npm run dev
```

## Docker

Copie `.env.example` para `.env`, ajuste apenas valores locais e execute:

```powershell
docker compose up --build
```

O serviço `migrations` aplica as migrations pendentes no PostgreSQL antes da
API iniciar. Não é necessário executar `dotnet ef database update` manualmente
para o fluxo local do Compose. O mesmo vale para banco recém-criado e para banco
já atualizado.

Se a API não iniciar por falha de migration, consulte a saída com:

```powershell
docker compose ps -a migrations
docker compose logs migrations
```

Corrija a migration ou a configuração indicada no log e inicie novamente com
`docker compose up --build`. A API só inicia depois que o serviço `migrations`
terminar com sucesso. Para executar migrations manualmente fora do Compose,
consulte [as instruções do projeto de migrations](src/ZFome.Infrastructure/Persistence/Write/Migrations/README.md).

Serviços locais:

- Web (Quasar em modo desenvolvimento com hot reload): `http://localhost:9000`
- API: `http://localhost:8080`
- PostgreSQL: `localhost:5432`

O `docker-compose.override.yml` é carregado automaticamente no desenvolvimento:
ele monta `web/ZFome.Web` no container e inicia `quasar dev`, refletindo
alterações de Vue/TypeScript sem reconstruir a imagem. Em hosts Windows, o watcher
usa polling para detectar alterações nos arquivos montados. Para executar o
frontend estático de produção definido no `docker-compose.yml`, use explicitamente:

```powershell
docker compose -f docker-compose.yml up --build
```

## Arquitetura

- `ZFome.Domain`: modelo e regras de domínio, sem dependências externas.
- `ZFome.Application`: Commands, Queries, handlers e portas.
- `ZFome.Infrastructure`: EF Core, Dapper e adapters técnicos.
- `ZFome.Contracts`: contratos externos explícitos.
- `ZFome.Api`: composition root e borda HTTP.

EF Core atende escrita e migrations; Dapper atende leitura. MediatR e AutoMapper são proibidos. Consulte `openspec/architecture.md`, `openspec/conventions.md` e os ADRs em `openspec/decisions/`.

## Cérebro do projeto

O conhecimento de produto, domínio, arquitetura, fluxos e orientações para agentes do zFome está organizado no cofre Obsidian **Cérebro do Arquiteto**, no repositório `Obsidian`, em `Arquiteturando/Projetos/zFome/`. Quando o cofre estiver disponível, desenvolvedores e agentes devem consultá-lo no início de uma tarefa para entender o contexto e navegar até as notas relacionadas. Comece por `00 - Home/Home.md` ou `00 - Home/Mapa do Projeto.md`; a nota `Arquiteturando/Bússola de desenvolvimento para agentes.md` reúne as orientações gerais para agentes.

O Cérebro e a memória disponível ajudam a compreender o projeto e seu histórico. Para requisitos vigentes e decisões normativas, confirme sempre no `AGENTS.md`, nas specs e changes do OpenSpec, nos ADRs, no Design System e no código atual, conforme aplicável. Se houver divergência, prevalecem essas fontes oficiais. As notas podem ficar desatualizadas; confirme informações voláteis no repositório.

## Configuração

Configurações não sensíveis ficam em `appsettings*.json`. Valores por ambiente usam variáveis com separador `__`, por exemplo `ConnectionStrings__zFome`. Senhas reais, tokens e arquivos `.env` não devem ser versionados.
