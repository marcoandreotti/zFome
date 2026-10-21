# Fundação web administrativa

## Entrega

Área administrativa implementada: sessão, usuários, catálogo, clientes/endereços, cupons e formas de pagamento. As telas possuem pesquisa, paginação, confirmação de operações sensíveis e preservação de formulários em erro.

- `/login`: código de contexto, senha e desafio enviado por e-mail. A senha é descartada após a primeira etapa; solicitar outro código reinicia o login e exige a senha novamente.
- `/change-password`: troca obrigatória de senha temporária, seguida de novo login.
- `/administration`: shell responsivo, unidade autorizada, visão de contexto e saída.
- `/administration/foundation`: mantém a página de fundação existente.
- `/administration/users`: pesquisa paginada, cadastro, edição de nome, estado, papéis e acesso à unidade selecionada. A rota exige capacidade `administration`.
- `/access-denied`: rejeição visual para perfis sem capacidade de gestão.

## Sessão e isolamento

Pinia mantém o contexto apenas em memória. `sessionStorage` guarda somente `zf-unit`, revalidado contra a lista retornada pelo servidor. Cookies de acesso/renovação não são lidos pelo JavaScript. O cookie CSRF é enviado no cabeçalho correspondente.

Axios serializa renovação e tenta novamente apenas uma vez. Uma revisão de contexto invalida respostas que chegaram depois da troca de unidade ou do logout. O conteúdo administrativo é remontado na troca de revisão. A API continua responsável pela autorização em todas as operações.

## Execução local

Em `web/ZFome.Web`, executar `npm run dev`. O Quasar usa a porta 9000 e encaminha `/api` para `http://localhost:8080`; `API_PROXY_TARGET` altera esse destino. Em produção, servir web e `/api` na mesma origem, com HTTPS e fallback de rotas da SPA. Não configurar origem cruzada sem revisar CORS, cookies e CSRF.

## Verificação final — 2026-09-04

- `npm test`: 44 testes aprovados em nove arquivos, incluindo estados de erro/recuperação, componentes, sessão, unidade, navegação e restrições de Owner.
- `dotnet test ZFome.sln --no-restore`: 227 testes aprovados (60 Domain, 64 Application, 7 Architecture e 96 Integration/API), sem falhas ou ignorados. As suítes de API exercitam chamadas diretas negadas, isolamento e persistência; os testes visuais não substituem essas verificações.
- `dotnet build ZFome.sln --no-restore`: zero erros e warnings.
- `npm run typecheck` e `npm run build`: aprovados.
- Formatação Prettier dos arquivos frontend da mudança e `dotnet format whitespace` dos arquivos C# alterados verificadas.
- `scripts/check-administration-browser.mjs`: Chrome com aplicação compilada e respostas HTTP simuladas, em 1440×1024 e 768×1024. Verifica ausência de overflow da página, abertura por teclado, foco dentro dos diálogos, fechamento por Escape, foco ao navegar, troca de unidade, restrições de Admin/cozinha/atendimento e preservação de formulário em conflito. Mede contraste mínimo de 4,5:1 nos botões principais e alertas.
- Capturas em `TestResults/administration-browser` inspecionadas para catálogo, usuários, clientes, cupons e pagamentos. Esta verificação não representa um percurso navegador/API real com MFA e envio de e-mail.

Para repetir o roteiro, executar após o build: `node scripts/check-administration-browser.mjs`. Requer Playwright disponível; `PLAYWRIGHT_MODULE` pode apontar para o módulo instalado no ambiente e `BROWSER_EXECUTABLE` para Chrome/Chromium. `BROWSER_CHECK_OUTPUT` altera o diretório das capturas.

## Gestão de usuários e proteção de Owner

Somente outro Owner ativo pode conceder/remover Owner e ativar/desativar usuários que possuam esse papel. Admin não pode se promover, promover terceiros, retirar Owner ou alterar o estado de Owner, mesmo inativo. Owner não pode alterar seu próprio papel Owner ou estado. Os poderes globais de PlatformSuperUser permanecem separados; a proteção do último Owner ativo também se aplica à plataforma.

As escritas revalidam ator e unidade dentro de transação com bloqueio por Tenant. A contagem e a escrita preservam ao menos um Owner ativo e um administrador ativo com acesso a unidade ativa, incluindo concorrência. A remoção do último papel também é rejeitada. Operações negadas não persistem alterações parciais.

As operações ficam em `/api/admin/establishments/{establishmentId}/users`: GET paginado, POST cadastro, PUT `/{userId}` para nome, PATCH `/{userId}/active`, PUT `/{userId}/roles/{role}` e PUT `/{userId}/access`. Cadastro recebe senha inicial, papel e associa a unidade selecionada; e-mail não é alterado pelo formulário de edição. A senha não é armazenada pelo navegador. Papel usa os IDs 1–6 já existentes.

GET aceita `search`, `isActive`, `associatedOnly`, `page` (1–1.000.000) e `pageSize` (1–100; padrão 20). Lista somente usuários do Tenant resolvido; associações retornadas se limitam à unidade selecionada. Não expõe hash, credenciais ou TenantId. `isCurrentUser` permite refletir a restrição de autoalteração sem expor claims. Respostas usam `no-store`; operações globais concluídas registram ator, método, caminho e correlação no log estruturado.

## Catálogo e demais módulos

- `/administration/catalog`: categorias, produtos, variações, imagens, adicionais e grupos; seleção paginada preserva vínculos inativos. As consultas independentes GET `catalog/additionals` e `catalog/additional-groups` incluem recursos sem vínculos, ativos e inativos, com pesquisa e paginação. O cardápio público mantém seu contrato e filtros.
- `/administration/customers`: pesquisa e edição de contatos e endereços, definição de principal e confirmação de remoção.
- `/administration/coupons`: pesquisa, validade, desconto, limites e ativação/desativação.
- `/administration/payment-methods`: pesquisa, edição e ativação/desativação.

ProblemDetails associa validações aos campos quando possível, mantém os dados editados e apresenta mensagem específica para 400/401/403/404/409 mesmo sem descrição do servidor. Listagens oferecem tentativa de recuperação. O seletor do catálogo descarta resultados anteriores quando a consulta falha, evitando selecionar dados desatualizados.