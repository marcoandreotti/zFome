# Tasks

## 1. Domínio de avaliações

- [x] 1.1 Criar `Rating`, `Review`, estados e invariantes para nota, comentário, escopo, moderação e janela; verificar testes unitários de 1–5, notas baixas, normalização e transições.

## 2. Persistência e resumo

- [x] 2.1 Mapear schema `reviews`, review, request idempotente, histórico de moderação e resumo com constraints/índices tenant-first; verificar `ModelConventionsTests`.
- [x] 2.2 Implementar migration aditiva e testes de criação do schema, unicidade por pedido/alvo e isolamento de tenant; aplicar em banco descartável limpo.
- [x] 2.3 Implementar repositórios de escrita e gateways Dapper de situação, comentários, moderação e resumos; verificar consulta tenant-scoped e sem N+1.

## 3. Casos de uso

- [x] 3.1 Implementar publicação atômica e idempotente, conferindo pedido concluído, janela e pertencimento dos produtos; verificar replay, payload conflitante e alvo inválido em testes Application.
- [x] 3.2 Implementar consulta de elegibilidade/situação, listagem pública, resumo e paginação administrativa; verificar unidade resolvida no servidor e somente registros publicados em respostas públicas.
- [x] 3.3 Implementar reprovar pendentes e ocultar/restaurar publicadas com motivo e histórico append-only; verificar autorização delegada à API, auditabilidade e atualização síncrona dos resumos.

## 4. API e contratos

- [x] 4.1 Criar DTOs, validação e endpoints públicos de situação, publicação, listagem e resumos; verificar metadados HTTP e erros 400/404/409/422 em integração.
- [x] 4.2 Criar endpoints administrativos paginados para aprovar/reprovar pendentes e ocultar/restaurar publicadas sob policy `Management`; verificar rejeição sem policy/motivo e isolamento tenant/unit.
- [x] 4.3 Estender contratos e read model do catálogo para retornar média/contagem por produto; verificar endpoint público e ausência de consulta por item.

## 5. Experiência pública

- [x] 5.1 Adicionar `q-rating` com nota textual, comentário condicional e estado de envio na página de avaliação; verificar teclado, rótulos, limite e mensagens em testes frontend.
- [x] 5.2 Oferecer convite apenas em pedido concluído dentro do prazo e preservar acompanhamento; verificar estados elegível, inelegível, expirado e já avaliado.
- [x] 5.3 Exibir média/contagem da unidade e de produtos e atualizar tipos/client; verificar estados com e sem avaliações.

## 6. Administração

- [x] 6.1 Criar página administrativa de avaliações com filtros, paginação e detalhe anônimo; verificar estados vazio/erro/carregamento e policy de navegação.
- [x] 6.2 Implementar aprovação/reprovação de pendentes e ocultar/restaurar publicadas com justificativa; verificar estado `Rejected` irrestaurável, estado público apenas após aprovação, persistência do motivo e ausência de edição de nota/exclusão.

## 7. Integração final

- [x] 7.1 Executar OpenSpec validate, testes Domain/Application/Integration, testes frontend e typecheck; resolver regressões introduzidas pela change e registrar falhas preexistentes com evidência.
- [x] 7.2 Revisar diff, migration e docs de API afetadas; verificar Definition of Done e atualizar tarefas com o estado real.

> Evidências: suíte .NET completa aprovada (222 testes), build sem erros, testes específicos de reviews aprovados e typecheck frontend aprovado. A execução completa do Vitest no sandbox atual falha por `ENOENT` ao recuperar módulos lazy do Vite; os testes específicos de reviews haviam sido aprovados antes dessa falha ambiental.

