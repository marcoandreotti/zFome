# Inventário da administração web

## Change e estado

`build-administration-web`, tarefa 1.1 concluída após inventário, correções autorizadas da dependência e testes de regressão. Progresso: 1/12; frontend ainda não alterado.

## Reuso identificado

- `web/ZFome.Web`: Vue, Quasar e TypeScript; Axios e Pinia já declarados. Não introduzir outro framework de componentes ou estado.
- `src/layouts/AdministrationLayout.vue`: shell administrativo inicial.
- `src/router/routes.ts`: rotas lazy existentes; preservar página de fundação.
- `src/boot/http.ts`: cliente Axios e contrato de ProblemDetails existentes; ainda sem cookies, renovação serializada ou store de sessão.
- `src/themes/_tokens.scss`, `src/themes/tenant-theme.ts` e `src/css/app.scss`: tema e tokens existentes.
- `ZFome.Application/Identity/AdministrativePolicies.cs`: mapa autoritativo de capacidades; administração de usuários não deve ser concedida a todo papel de gestão indistintamente.
- `ZFome.Api/Authentication/AuthenticationEndpoints.cs`: início/conclusão de MFA, renovação, logout, troca de senha e gestão de identidades de plataforma.
- `ZFome.Api/Administration/AdministrationEndpoints.cs`: clientes, pedidos, cupons e pagamentos por estabelecimento.

## Lacunas identificadas no levantamento inicial

1. Não existe endpoint de consulta do contexto autenticado que forneça sessão, capacidades e unidades autorizadas ao navegador. Os endpoints de autenticação retornam somente desafio ou expirações/obrigação de troca; o frontend não pode obter claims a partir dos cookies HttpOnly.
2. `RefreshAuthenticationCommandHandler` chama `AuthenticationTokenFactory`, que revalida atividade somente para identidades de plataforma. Um usuário administrativo desativado pode receber novas credenciais de renovação, embora o resolver de acesso rejeite o usuário inativo. Isso diverge da exigência de rejeitar renovações.
3. `ChangeTemporaryPasswordCommandHandler` revoga somente a família atual. A spec exige revogar as sessões anteriores da identidade após a troca da senha temporária.
4. `BeginAuthenticationCommandHandler` cria novo desafio sem invalidar os anteriores ou aplicar intervalo de reenvio por identidade. A contagem atual considera desafios por origem, não tentativas de senha inválida.

## Correções autorizadas e verificadas

- `GET /api/auth/context` consulta o contexto persistido, retorna capacidades e unidades autorizadas e usa `Cache-Control: no-store`. Não retorna tokens, TenantId ou claims internas. Sessões restritas recebem apenas flags, sem unidades ou capacidades.
- Renovação e conclusão do MFA revalidam usuário administrativo e Tenant ativos. Credenciais novas não são emitidas para identidade inelegível; suas sessões são revogadas.
- Troca de senha temporária revoga todas as famílias e desafios pendentes da identidade, sem afetar outros usuários.
- Reenvio respeita `Authentication:ResendInterval` (um minuto por padrão), invalida desafios anteriores e serializa emissões concorrentes da identidade no PostgreSQL.
- Início e conclusão do login possuem limite HTTP de 20 solicitações por minuto por endereço remoto, incluindo tentativas inválidas. Esse limite é por instância da API; a proteção de reenvio por identidade é coordenada no banco. Uma implantação com proxy deve configurar o endereço remoto confiável e considerar limitação agregada na borda.
- Nenhuma migration necessária; nenhuma alteração no change arquivado.

## Verificação

- Domain: 54 testes aprovados.
- Application: 54 testes aprovados.
- Architecture: 7 testes aprovados.
- Integration/API: 83 testes aprovados, incluindo PostgreSQL real em Docker.
- Total: 198, sem falhas ou ignorados. Build completo com zero erros e warnings.
- Formatação limitada aos arquivos desta correção, preservando a dívida de formatação fora do escopo.

Próxima tarefa do change: shell administrativo e rotas lazy (1.2).
