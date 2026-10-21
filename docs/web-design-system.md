# Design System web do zFome

## Propósito

Este documento é a fonte oficial das decisões visuais e dos padrões de componentes da aplicação web. Ele orienta implementação e revisão, sem substituir as specs funcionais em `openspec/specs/`.

O objetivo é permitir que qualquer pessoa ou agente descubra como o zFome já resolve uma necessidade antes de criar uma solução nova.

## Fontes oficiais

As fontes devem ser consultadas em conjunto:

- Este documento define propósito, critérios de uso e padrões visuais.
- `web/ZFome.Web/src/themes/_tokens.scss` contém os tokens implementados.
- `web/ZFome.Web/src/themes/tenant-theme.ts` e `web/ZFome.Web/src/modules/public-ordering/theme.ts` contêm a aplicação programática de tema.
- `web/ZFome.Web/src/css/app.scss` contém estilos globais ainda vigentes.
- `web/ZFome.Web/src/components/` contém componentes compartilhados implementados.
- `web/ZFome.Web/src/layouts/` contém os shells oficiais das superfícies.
- Componentes reutilizáveis de feature permanecem em `web/ZFome.Web/src/modules/<module>/`.
- Specs de frontend em `openspec/specs/` definem comportamento, acessibilidade e resultados obrigatórios.

Se documentação e implementação divergirem, não escolher silenciosamente. Confirmar se o código representa dívida, se o documento está desatualizado ou se a mudança exige decisão explícita.

## Princípios

1. Pesquisar antes de criar.
2. Reutilizar por semântica, não apenas por semelhança visual.
3. Usar Quasar como matéria-prima e não como identidade visual final.
4. Criar abstração compartilhada somente para um padrão estável e comprovado.
5. Priorizar tokens semânticos em vez de valores arbitrários.
6. Tornar estados, ações e hierarquia compreensíveis antes de decorar a interface.
7. Preservar acessibilidade e responsividade como requisitos, não como acabamento.
8. Evoluir incrementalmente sem exigir refatoração visual fora do escopo da feature.

## Superfícies oficiais

### Public

Atende o cliente do estabelecimento. Seu propósito é transformar descoberta em pedido válido com pouco atrito.

Prioridades:

- mobile-first;
- identidade visual do Tenant;
- fotografia e reconhecimento do produto;
- busca, categorias e descoberta;
- preço, disponibilidade e prazo evidentes;
- carrinho persistentemente acessível;
- composição de produtos complexos sem esconder requisitos;
- checkout direto e acompanhamento compreensível.

O tema público pode variar por Tenant, mas deve manter contraste, foco e legibilidade. Tokens de marca pública devem ter escopo da superfície e não devem alterar silenciosamente Administration, Operations, KDS ou Platform.

### Administration

Atende proprietários, gerentes e pessoas autorizadas a configurar o estabelecimento. Seu propósito é reduzir esforço cognitivo e operacional na manutenção do negócio.

Prioridades:

- navegação consistente;
- cabeçalhos e ações de página previsíveis;
- formulários claros;
- tabelas e filtros eficientes;
- densidade adequada ao trabalho;
- estados e confirmações inequívocos;
- indicadores somente quando apoiam uma decisão.

A marca do Tenant pode aparecer como identificação ou accent discreto. Ela não controla a linguagem visual administrativa.

### Operations

Atende equipes que acompanham e avançam pedidos. Seu propósito é reduzir o tempo entre perceber uma mudança e executar a ação correta.

Prioridades:

- estados e próxima ação evidentes;
- tempo decorrido e horário prometido visíveis;
- alto contraste e leitura rápida;
- poucos cliques;
- conexão, sincronização e defasagem perceptíveis;
- uso por teclado, monitor, tablet e touch;
- separação entre trabalho imediato e demanda futura.

### KDS

Atende a produção da cozinha. Seu propósito é preservar sequência, prioridade e composição em ambiente de pressão.

Prioridades:

- leitura à distância;
- quantidades, modificadores e observações em destaque;
- atraso comunicado por mais de um sinal;
- ações grandes e inequívocas;
- mínimo de navegação e distração;
- funcionamento em tela cheia e tablet quando aplicável.

### Platform

Atende identidades globais de provisionamento. Seu propósito é tornar inequívoco que o usuário está fora do contexto operacional de um Tenant.

Prioridades:

- identidade institucional consistente;
- separação visual entre contexto global e unidade;
- ações de provisionamento cuidadosamente confirmadas;
- estados vazios orientados ao próximo passo.

## Layouts oficiais

- `PublicLayout.vue`: shell do cardápio, checkout e acompanhamento público.
- `AdministrationLayout.vue`: shell tenant-scoped de gestão.
- `OperationsLayout.vue`: shell de pedidos, entrega e cozinha.
- `PlatformLayout.vue`: shell global de provisionamento.

Cada shell expõe um identificador estável no seu elemento raiz: `data-surface="public"`,
`data-surface="administration"`, `data-surface="operations"` ou
`data-surface="platform"`. Temas de Tenant são aplicados somente ao subtree
`public`; nunca devem escrever variáveis em `document.documentElement`.

Não criar um layout genérico para unificar superfícies com propósitos diferentes. Extrações compartilhadas são aceitáveis quando preservam identidade, navegação e densidade próprias.

## Tokens

### Tokens implementados

`_tokens.scss` define atualmente:

- marca: `--zf-brand-primary`, `--zf-brand-primary-hover`,
  `--zf-brand-primary-text`, `--zf-brand-on-primary`, `--zf-brand-soft`,
  `--zf-brand-secondary` e `--zf-brand-accent`;
- navegação: `--zf-navigation-background`, `--zf-navigation-text` e
  `--zf-navigation-muted`;
- superfícies: `--zf-surface-page` e `--zf-surface-raised`;
- texto: `--zf-text-primary` e `--zf-text-muted`;
- borda: `--zf-border-subtle`;
- foco: `--zf-focus-ring`;
- estados: `--zf-status-success`, `--zf-status-success-text`,
  `--zf-status-warning`, `--zf-status-warning-text`, `--zf-status-danger`,
  `--zf-status-urgency`, `--zf-status-info`, `--zf-status-confirmed`,
  `--zf-status-preparing`, `--zf-status-ready` e `--zf-status-cancelled`;
- raio-base;
- família tipográfica.

### Paleta do produto

| Papel | Valor | Aplicação |
| --- | --- | --- |
| Primary | `#F97316` | CTAs, seleção e detalhes de marca |
| Primary hover | `#EA580C` | Interação de CTAs |
| Primary text | `#C2410C` | Texto de marca legível em fundo claro |
| Primary on-color | `#111827` | Texto em fundo primary/hover, preservando contraste |
| Primary soft | `#FFF7ED` | Seleções e realces suaves |
| Graphite | `#1F2937` | Navegação administrativa e estrutura |
| Text / muted | `#374151` / `#6B7280` | Conteúdo e apoio |
| Page / surface / border | `#F8FAFC` / `#FFFFFF` / `#E5E7EB` | Superfícies de trabalho |
| Success / warning / danger / info | `#16A34A` / `#F59E0B` / `#DC2626` / `#2563EB` | Estados semânticos |

O laranja é uma cor de ação e marca, não um fundo geral. Administration prioriza
grafite e neutros, usando laranja de forma pontual. Public mantém tema de Tenant
isolado e prioriza identidade, imagens e descoberta. Operações/KDS distinguem os
estados do pedido independentemente da cor de marca: confirmado em azul/índigo,
preparo em âmbar, pronto/concluído em verde e cancelado/rejeitado em vermelho.

Texto dentro de CTAs laranja usa grafite escuro. Labels sobre tons warning/success
usam variantes textuais mais escuras (`--zf-status-warning-text` e
`--zf-status-success-text`), enquanto os valores semânticos base podem ser usados
em fundos suaves, bordas ou indicadores. Cor nunca é o único sinal do estado.

Esses tokens são oficiais enquanto existirem no código, mas formam apenas a fundação inicial.
Os estados de urgência e erro devem ser acompanhados por texto ou ícone; a cor nunca
é o único sinal.

### Aparência clara e escura

A aplicação oferece três preferências: **Sistema**, **Claro** e **Escuro**. Sistema é o
padrão inicial e acompanha `prefers-color-scheme`; Claro e Escuro são escolhas explícitas.
A preferência fica no armazenamento local do navegador (`zfome.appearance-mode`) e é
sincronizada entre abas abertas no mesmo navegador. Ela não depende da conta nem é
sincronizada entre dispositivos.

`src/themes/_tokens.scss` define os mesmos tokens semânticos nas duas aparências. A raiz
`data-theme` escolhe a paleta, enquanto o boot `appearance` e o plugin Dark do Quasar
mantêm páginas, controles e overlays na mesma escolha. Novos estilos devem consumir os
tokens (`--zf-surface-*`, `--zf-text-*`, `--zf-border-*`, `--zf-status-*`) em vez de
fixar branco, preto ou cinzas de uma única aparência.

O modo escuro preserva o grafite e o laranja da marca, mas usa superfícies elevadas,
texto e variantes semânticas com contraste apropriado. Public pode aplicar as cores e a
tipografia do Tenant somente em Claro; em Escuro mantém superfícies escuras e adapta as
cores de marca para preservar legibilidade. Essa personalização não altera os tokens
globais nem as outras superfícies.

### Compatibilidade

Os aliases legados `--zf-color-*` foram removidos depois da migração dos últimos
consumidores e da verificação das quatro superfícies. Todo estilo novo usa diretamente
os tokens semânticos implementados. Antes de remover ou renomear um token oficial,
pesquisar todos os consumidores e verificar Public, Administration, Operations, KDS e
Platform.

### Critérios para novos tokens

Antes de adicionar cor, espaçamento, raio, sombra, tipografia, breakpoint ou z-index:

1. verificar se já existe token com a mesma semântica;
2. verificar se o valor representa decisão reutilizável;
3. escolher nome pelo papel, não pela aparência ou por uma página específica;
4. documentar o token quando seu uso não for autoexplicativo;
5. validar contraste e impacto nas superfícies aplicáveis.

Evitar tokens como `--catalog-card-margin-17`. Preferir papéis estáveis, por exemplo superfície elevada, texto secundário, espaço médio ou raio de card, quando esses conceitos forem realmente adotados.

Valores locais continuam aceitáveis para ajustes intrínsecos e não reutilizáveis. Não criar token apenas para eliminar todo número do CSS.

### Evolução prevista

As categorias abaixo são candidatas, não tokens já implementados:

- cor semântica de info e novos estados operacionais que tenham consumidor real;
- escala de spacing;
- escala tipográfica;
- raios por papel;
- elevações;
- motion e duração;
- camadas e z-index;
- densidades public, administration e operations.

### Acento analítico da Administration

A superfície Administration utiliza um acento violeta de apoio para navegação selecionada,
identificação do contexto gerencial e gráficos do dashboard, inspirado em interfaces de
analytics. Ele é definido no escopo `[data-surface='administration']` como
`--zf-admin-accent`, `--zf-admin-accent-soft` e `--zf-admin-accent-text`, com variantes
para tema escuro. O acento não substitui o laranja de marca nos CTAs nem altera Public,
Operations, KDS ou Platform. Superfícies administrativas seguem claras e elevadas no
tema claro, com fundo lavanda muito suave, cartões brancos e contornos discretos.

Uma feature não deve introduzir toda a escala antecipadamente. Adicionar somente decisões exigidas por casos concretos.

## Classificação de componentes

### Global

Representa padrão estável usado por múltiplas features ou superfícies. Deve permanecer em `src/components/` ou em uma futura pasta oficial do Design System, caso essa organização seja aprovada.

Requisitos:

- propósito e limites claros;
- semântica estável;
- mais de um consumidor concreto ou padrão transversal evidente;
- uso de tokens oficiais;
- API pequena e coerente;
- documentação neste catálogo;
- testes quando houver comportamento próprio.

### Domain ou Feature

Representa conceito reutilizável dentro de um domínio, como produto, pedido ou cliente. Deve permanecer próximo ao módulo consumidor.

Exemplos conceituais: `ProductCard`, `OrderCard`, `OrderTimeline`, `ModifierSelector` e `CustomerAddressCard`. Esses nomes não indicam que os componentes já existam ou estejam aprovados globalmente.

### Page-specific

Organiza apenas uma página e não representa um padrão reutilizável. Deve permanecer ao lado da página ou dentro dela quando a complexidade for pequena.

Não promover componentes automaticamente. Também não aumentar indefinidamente props e slots de um componente existente para acomodar responsabilidades diferentes.

## Catálogo atual

### Componentes compartilhados implementados

#### ProblemBanner

- Caminho: `web/ZFome.Web/src/components/ProblemBanner.vue`.
- Propósito: apresentar erros HTTP e ProblemDetails de forma consistente, mantendo espaço para uma ação de recuperação.
- Usar quando: uma página, painel ou operação precisa comunicar falha recuperável ou erro conhecido da API.
- Não usar quando: o erro pertence exclusivamente a um campo de formulário ou uma mensagem transitória de sucesso é suficiente.

#### AppearanceControl

- Caminho: `web/ZFome.Web/src/components/AppearanceControl.vue`.
- Classificação: Global; controle único de preferência visual usado pelos shells oficiais.
- Propósito: escolher Sistema, Claro ou Escuro e apresentar a opção atual.
- Usar uma vez no cabeçalho de Public, Administration, Operations/KDS e Platform.
- A preferência e a aplicação dos tokens pertencem a `src/themes/appearance.ts`; não
  duplicar estado ou persistência dentro dos layouts.
- Não substituir o tema institucional de Operations/KDS nem expandir cores do Tenant
  para fora do subtree Public.

Não existem atualmente `AppButton`, `AppCard`, `AppDialog`, `AppStatus`, `AppPageHeader` ou equivalentes oficiais. Antes de criar qualquer um deles, comprovar o padrão, pesquisar consumidores e documentar a decisão aqui.

### Componentes de feature identificados

- Public ordering:
  - `PublicUnitContext.vue`: identidade e contexto público da unidade;
  - `PublicCatalogSearch.vue`: busca rotulada do cardápio;
  - `PublicCategoryNavigation.vue`: navegação horizontal e seleção de categoria;
  - `PublicProductCard.vue`: apresentação e seleção de oferta pública;
  - `PublicCartAccess.vue`: acesso persistente ao carrinho com quantidade e total.
- Administration:
  - `CatalogPicker.vue`: seleção paginada de vínculos dentro do catálogo;
  - `CatalogCompactTable.vue`: manutenção compacta dos recursos do catálogo;
  - `CatalogProductVisualGrid.vue`: reconhecimento visual de produtos sobre o mesmo estado da visão compacta;
  - `AccessStep.vue`: etapa de acesso no onboarding administrativo.
- Operations:
  - `OperationsOrderCard.vue`: resumo executável do pedido e sua próxima ação;
  - `OperationsSyncStatus.vue`: conexão, fallback, defasagem e recuperação da fila.
- KDS:
  - `KitchenColumn.vue`: agrupamento de tickets por etapa de produção;
  - `KitchenTicketCard.vue`: ticket de produção com prioridade, atraso, composição e ação touch.

Eles permanecem próximos aos módulos porque suas responsabilidades são específicas. Reuso fora desses domínios exige nova avaliação, não simples movimentação de pasta.

### Padrões administrativos comprovados

As classes `admin-page`, `admin-page-header` e `admin-page-eyebrow` normalizam
contenção, hierarquia e ação principal nas páginas administrativas. Elas são política
visual da superfície, não um componente global configurável. Catálogo, clientes,
usuários, cupons, pagamentos, disponibilidade, entregas, onboarding e visão geral são
consumidores concretos. Não criar `AppPageHeader` enquanto não houver comportamento
compartilhado além dessa composição semântica e responsiva.

### Layouts

Os quatro layouts listados na seção Layouts oficiais fazem parte do catálogo e não devem ser substituídos por um `MainLayout` único.

## Uso do Quasar

Antes de usar diretamente `QBtn`, `QDialog`, `QCard`, `QInput`, `QTable`, `QBanner` ou outro componente:

1. procurar componente oficial com a semântica necessária;
2. verificar padrões já usados na mesma superfície;
3. usar o componente compartilhado quando houver linguagem ou comportamento oficial;
4. usar Quasar diretamente quando a estrutura for local e não houver padrão global;
5. não criar wrapper que apenas replique props sem acrescentar semântica, comportamento ou consistência.

## Padrões de experiência

### Ações em coleções e confirmações

- Ações contextuais repetidas em tabelas, grids, listas e coleções de formulário usam `QBtn` quadrado (`square`) com ícone, alvo mínimo de 44 × 44 px e a classe `collection-action-btn`. Quando a ação exibe somente um ícone, usa a variante `flat` e peso e tamanho reforçados. Botões com rótulo visível mantêm seu tratamento próprio.
- Botões apenas com ícone devem ter `aria-label`; use `QTooltip` com a mesma ação em linguagem clara. A dica complementa o nome acessível e não substitui uma ação que precise permanecer visível.
- Escolha cor e ícone de acordo com a consequência: ações comuns usam `primary`/`secondary`; ativar ou reativar usa `positive`; desativar, remover ou revogar usa `negative`. Estado e ação também devem ser expressos por texto acessível, não somente pela cor.
- Em Operations/KDS, mantenha visível o rótulo da transição junto ao ícone quando a equipe precisa reconhecer a próxima etapa rapidamente.
- Ações primárias que salvam, criam, enviam ou confirmam usam ícone à esquerda e rótulo visível que nomeia a operação (por exemplo, “Salvar alterações”, “Adicionar região” ou “Confirmar pedido”). Preserve loading, disabled e o esquema semântico de cor.
- Confirmações de efeitos destrutivos nomeiam o efeito no próprio botão (“Remover endereço”, “Desativar cupom”); evite o rótulo genérico “Confirmar” quando a ação puder ser descrita diretamente.
- Botões de navegação, voltar/fechar, pesquisa, atualização e recuperação mantêm o tratamento apropriado à sua função; não recebem aparência de ação de coleção por estarem próximos dela.
- Use ícones Material já incluídos pelo projeto, sem adicionar uma biblioteca ou wrapper genérico de botão.

### Estados de dados

Toda experiência assíncrona deve avaliar:

- loading: informar atividade e preservar geometria com skeleton quando isso reduzir salto visual;
- success: confirmar resultado na intensidade adequada;
- empty: explicar a ausência e oferecer próxima ação quando existir;
- error: explicar, preservar dados úteis e oferecer recuperação;
- disabled: tornar o motivo compreensível quando necessário;
- unauthorized ou forbidden: não exibir dados protegidos e orientar sem sugerir acesso inexistente.

### Formulários

- associar erros aos campos quando possível;
- preservar valores após validação, conflito ou falha recuperável;
- distinguir ação primária, secundária e destrutiva;
- confirmar ações sensíveis;
- não depender de placeholder como rótulo;
- organizar formulários longos em seções coerentes sem esconder dependências importantes.

### Navegação

- apresentar somente módulos compatíveis com as capacidades conhecidas, sem substituir autorização da API;
- manter contexto de unidade visível em Administration e Operations;
- reconstruir dados ao trocar unidade;
- manter foco previsível após navegação e fechamento de dialogs;
- usar breadcrumbs apenas quando comunicarem hierarquia real.

### Feedback

- usar mensagens próximas da ação que as originou;
- reservar dialogs para decisões, confirmações ou tarefas focadas;
- não usar notificação transitória como única forma de comunicar erro que exige ação;
- comunicar estado de sincronização em experiências em tempo real.

## Responsividade

Toda alteração deve ser avaliada em smartphone, tablet e desktop quando essas dimensões forem suportadas pela superfície.

- Public é mobile-first.
- Administration prioriza desktop e tablet, sem quebrar fluxos essenciais em larguras menores previstas pela spec.
- Operations e KDS devem considerar monitor, tablet e touch.
- Preferir CSS responsivo, grid, flex e recursos do Quasar a condicionais JavaScript de layout.
- Não considerar concluída uma tela validada apenas em desktop.

## Acessibilidade

Toda alteração deve preservar:

- navegação completa por teclado;
- foco visível e ordem de foco coerente;
- rótulos acessíveis;
- HTML semântico antes de ARIA;
- contraste adequado;
- estado comunicado por texto ou ícone além de cor;
- áreas de toque apropriadas;
- mensagens de erro perceptíveis;
- preferência por redução de movimento quando animações forem introduzidas.

## Fluxo para criar ou alterar uma tela

1. Ler a spec da capability.
2. Identificar a superfície e o layout oficial.
3. Pesquisar páginas e componentes semanticamente semelhantes.
4. Consultar tokens e padrões de estado.
5. Classificar novos componentes.
6. Implementar a menor extensão coerente.
7. Testar comportamento, estados e permissões.
8. Verificar responsividade e acessibilidade.
9. Atualizar este catálogo se um padrão global for criado ou alterado.

## Evolução do Design System

Não introduzir Storybook, biblioteca visual adicional ou gerador de documentação apenas para criar um catálogo. Este Markdown é suficiente enquanto permitir descoberta e revisão.

Quando o volume de componentes e variantes tornar a manutenção manual insuficiente, propor a ferramenta, seus consumidores, custo e estratégia de adoção antes de adicionar dependências.

Uma mudança global de tokens, temas, layouts ou APIs de componentes exige avaliação de impacto. Se alterar decisão arquitetural, registrar ADR antes da implementação.
