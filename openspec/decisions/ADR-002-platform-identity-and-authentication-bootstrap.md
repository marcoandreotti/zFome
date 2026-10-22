# ADR-002 — Identidade de plataforma e bootstrap de autenticação

## Contexto

O zFome isola usuários administrativos por Tenant e permite o mesmo e-mail em Tenants distintos. A plataforma precisa autenticar esses usuários com contexto explícito e também possuir operadores globais capazes de administrar todos os Tenants.

## Problema

Reutilizar um usuário tenant-scoped como superusuário criaria associações artificiais, claims ambíguas e risco de bypass do isolamento. O primeiro operador global também precisa existir sem senha versionada ou alteração manual do banco.

## Opções

### Opção A — Papel global no usuário administrativo

Mantém uma entidade, mas mistura autorização tenant-scoped e global e exige Tenant artificial ou semântica especial para `TenantId`.

### Opção B — Tenant interno da plataforma

Reutiliza o modelo atual, porém transforma um identificador de isolamento em exceção arquitetural e facilita consultas cross-Tenant acidentais.

### Opção C — Identidade global separada

Cria identidade de plataforma sem `TenantId`, com autenticação e auditoria explícitas, compartilhando apenas conceitos realmente comuns como e-mail e hashing.

## Decisão

Adotar a opção C. Usuários administrativos continuam tenant-scoped e fazem login com código público único do Tenant. Superusuários usam identidade global e código público de plataforma configurado. Nenhum código de contexto é fator secreto.

O primeiro superusuário será criado idempotentemente no startup a partir de secrets de implantação. A senha nasce temporária; após senha e MFA válidos, a sessão permite somente troca de senha e logout. A troca revoga sessões anteriores e exige novo login. Apenas outro superusuário plenamente autenticado administra seus pares, e o último ativo não pode ser desativado.

## Consequências

### Positivas

- autorização global não contamina o modelo multi-tenant;
- auditoria distingue atores tenant-scoped e de plataforma;
- bootstrap não exige credencial no código ou operação manual no banco;
- políticas podem negar explicitamente claims globais forjadas.

### Negativas

- dois tipos de identidade precisam ser resolvidos pelo fluxo de autenticação;
- migrations, sessões e testes ficam mais amplos;
- secrets de bootstrap exigem disciplina operacional após o primeiro acesso.
