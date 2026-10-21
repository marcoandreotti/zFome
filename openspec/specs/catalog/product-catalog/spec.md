# Product Catalog Specification

## Purpose

Define o catálogo vendável de cada unidade, incluindo hierarquia, variações, imagens e adicionais reutilizáveis.

## Requirements

### Requirement: Categorias formam uma hierarquia acíclica
Uma categoria SHALL pertencer a uma unidade, MAY possuir uma categoria pai da mesma unidade e MUST rejeitar vínculos que criem ciclo ou autorreferência.

#### Scenario: Categoria descendente usada como pai
- **WHEN** uma categoria for movida para baixo de uma de suas descendentes
- **THEN** o domínio MUST rejeitar a alteração e preservar a hierarquia anterior

#### Scenario: Categoria de outra unidade
- **WHEN** uma categoria pai pertencer a unidade diferente
- **THEN** o sistema MUST rejeitar o vínculo

### Requirement: Produto possui oferta base e variações opcionais
Cada produto SHALL pertencer a uma categoria da mesma unidade, possuir código único na unidade, preço base não negativo e MAY oferecer variações ativas com preço próprio não negativo.

#### Scenario: Variação selecionada
- **WHEN** um produto com variações for incluído em uma oferta
- **THEN** nome e preço da variação selecionada SHALL compor a oferta sem alterar o preço base cadastrado

### Requirement: Produto admite galeria ordenada
Um produto MAY possuir múltiplas imagens ordenadas e MUST possuir no máximo uma imagem principal.

#### Scenario: Segunda imagem principal
- **WHEN** uma imagem for marcada como principal e já existir outra principal
- **THEN** o sistema SHALL manter apenas uma imagem principal de forma consistente

### Requirement: Adicionais são reutilizáveis por unidade
Adicionais SHALL pertencer à unidade e MAY compor grupos vinculados a vários produtos; cada grupo MUST manter mínimo e máximo coerentes e somente referenciar adicionais da mesma unidade.

#### Scenario: Seleção fora dos limites
- **WHEN** a seleção de um grupo obrigatório não atender ao mínimo ou exceder o máximo
- **THEN** o sistema MUST rejeitar a composição do item

#### Scenario: Adicional cruzado
- **WHEN** um grupo tentar referenciar adicional de outra unidade
- **THEN** o sistema MUST rejeitar o vínculo

### Requirement: Itens inativos não são vendáveis
Categorias, produtos, variações, grupos ou adicionais inativos MUST NOT ser oferecidos para novas seleções públicas.

#### Scenario: Produto desativado após pedido anterior
- **WHEN** um produto for desativado
- **THEN** novos pedidos MUST NOT selecioná-lo e pedidos históricos SHALL permanecer legíveis

### Requirement: Catálogo pode ser administrado por unidade autorizada
O sistema SHALL permitir que atores autenticados com capacidade de gerenciamento criem, consultem e alterem categorias, produtos, imagens, variações, adicionais, grupos e seus vínculos somente na unidade operacional autorizada. O sistema MUST derivar o Tenant do contexto autenticado e MUST validar no servidor a associação ativa do usuário com a unidade, sem confiar em `TenantId` recebido do cliente.

#### Scenario: Gerente administra catálogo da unidade associada
- **WHEN** um gerente autenticado envia dados válidos para criar ou alterar um item na unidade à qual possui associação ativa
- **THEN** o sistema SHALL aplicar a alteração dentro do Tenant e da unidade resolvidos e SHALL retornar um contrato externo que não exponha entidades de domínio

#### Scenario: Tentativa administrativa cruzada
- **WHEN** um ator tenta consultar ou alterar catálogo de outro Tenant ou de unidade sem associação ativa
- **THEN** o sistema MUST negar a operação sem revelar a existência dos dados e MUST preservar o estado

#### Scenario: Entrada estruturalmente inválida
- **WHEN** uma operação administrativa contém campos obrigatórios ausentes, formatos inválidos ou referências incompatíveis com a mesma unidade
- **THEN** o sistema MUST rejeitar a operação com erro padronizado e MUST NOT persistir alteração parcial

### Requirement: Administração consulta a composição completa do catálogo
O sistema SHALL fornecer uma projeção administrativa ordenada do catálogo da unidade, incluindo itens ativos e inativos, hierarquia de categorias, produtos, imagens, variações, grupos e adicionais necessários para manutenção.

#### Scenario: Consulta administrativa da unidade
- **WHEN** um ator autorizado consulta o catálogo administrativo de sua unidade
- **THEN** o sistema SHALL retornar somente dados do Tenant e da unidade resolvidos, preservando hierarquia, ordenação, estados de ativação e vínculos cadastrados

#### Scenario: Navegação e visão visual dos produtos
- **WHEN** o ator autorizado administra o catálogo
- **THEN** as abas são apresentadas na ordem Categorias, Adicionais, Grupos de adicionais e Produtos; a visão Visual usa cards horizontais no padrão de leitura do cardápio, mantendo categoria, estado, descrição, preço, vínculos e ação de edição legíveis e responsivos

### Requirement: Cardápio público apresenta somente ofertas vendáveis
O sistema SHALL permitir consultar o cardápio público por slug ativo da unidade e SHALL retornar uma projeção hierárquica ordenada contendo somente categorias, produtos, variações, grupos e adicionais ativos. A resolução pública MUST determinar Tenant e unidade no servidor e MUST NOT conceder privilégios administrativos.

#### Scenario: Visitante consulta cardápio ativo
- **WHEN** um visitante consulta o slug de uma unidade e Tenant ativos
- **THEN** o sistema SHALL retornar categorias e ofertas vendáveis dessa unidade com preços, imagens, variações e adicionais ativos em sua ordem cadastrada

#### Scenario: Componentes inativos no catálogo
- **WHEN** a unidade possui categorias, produtos, variações, grupos ou adicionais inativos
- **THEN** esses componentes MUST NOT aparecer como novas opções no cardápio público, embora permaneçam disponíveis na projeção administrativa e em históricos que os referenciem

#### Scenario: Slug público indisponível
- **WHEN** o slug não existe ou pertence a Tenant ou unidade inativos
- **THEN** o sistema MUST NOT revelar dados de catálogo nem informações internas sobre a unidade

### Requirement: Alterações relacionais do catálogo são consistentes
Operações que alterem um aggregate do catálogo e suas coleções ou vínculos SHALL ser persistidas atomicamente e o armazenamento MUST rejeitar códigos duplicados na mesma unidade, referências cruzadas e relações estruturalmente inválidas.

#### Scenario: Falha ao persistir coleção do produto
- **WHEN** a persistência de uma imagem, variação ou vínculo de grupo falha durante alteração do produto
- **THEN** nenhuma parte da alteração desse aggregate SHALL permanecer persistida

#### Scenario: Código de produto repetido na unidade
- **WHEN** uma operação tenta persistir código de produto já utilizado na mesma unidade
- **THEN** o sistema MUST rejeitar a alteração por conflito sem modificar o produto existente

#### Scenario: Mesmo código em unidades diferentes
- **WHEN** unidades distintas utilizam o mesmo código de produto
- **THEN** o sistema SHALL permitir os cadastros mantendo isolamento entre seus catálogos

### Requirement: Oferta pode ficar temporariamente indisponível
O sistema SHALL permitir indisponibilizar produto ou opção por período ou até reativação, sem alterar seu cadastro nem seus históricos.

#### Scenario: Produto temporariamente indisponível
- **WHEN** o cardápio público é consultado durante a indisponibilidade
- **THEN** a oferta não pode ser adicionada a novo pedido e seu estado é comunicado conforme a política da unidade

### Requirement: Produto define grupos de modificadores configuráveis
O catálogo SHALL representar grupos tipados e tenant-scoped de escolhas, incluindo sabores, bordas, adicionais e remoções, com limites de seleção, quantidades, compatibilidades e uma estratégia de preço. A estratégia pertence ao grupo e SHALL ser uma das seguintes: `Additive`, `HighestPrice`, `Proportional` ou `NoPriceChange`. Grupos existentes SHALL ser migrados como `Additive`, sem mudar o comportamento de preço atual.

Cada associação de opção a grupo MAY definir regras para uma associação alvo (grupo + opção) do mesmo estabelecimento. A regra `Requires` SHALL ser direcional: selecionar a origem exige selecionar o alvo. A regra `Excludes` SHALL impedir a seleção conjunta das duas opções, independentemente da direção do cadastro. Produtos SHALL associar os grupos necessários para satisfazer todas as referências de suas regras. Referências inexistentes, cruzadas entre tenants/estabelecimentos ou incompatíveis com os grupos do produto SHALL ser rejeitadas.

`Additive` SHALL calcular o valor do grupo pela soma do preço vigente de cada opção multiplicado por sua quantidade selecionada. `HighestPrice` SHALL usar o maior preço vigente entre as opções selecionadas, sem ponderação por fração. `Proportional` SHALL somar o preço vigente de cada opção multiplicado por sua fração exata da unidade composta. `NoPriceChange` SHALL produzir valor zero. Valores proporcionais SHALL ser calculados com frações exatas e convertidos para moeda arredondando uma única vez no total do grupo, conforme a regra de `Money`.

Grupos `HighestPrice` e `Proportional` SHALL representar um único grupo de composição integral. Opções selecionadas nesses grupos SHALL informar frações positivas como numerador/denominador, sem aproximação decimal, e uma quantidade por opção. Quando configurada como integral, a soma exata das frações SHALL ser igual a 1. O catálogo SHALL permitir inicialmente composições 1/1, 1/2 + 1/2, três opções de 1/3 e quatro opções de 1/4. Grupos `Additive` SHALL usar quantidades e não frações. Um produto SHALL associar no máximo um grupo `HighestPrice` ou `Proportional`; grupos `Additive` e `NoPriceChange` não concorrem para definir o preço composto.

#### Scenario: Combinação incompatível
- **WHEN** uma composição viola limite ou compatibilidade configurada
- **THEN** o sistema rejeita a seleção sem alterar o produto cadastrado

#### Scenario: Grupo de composição substitui preço-base
- **WHEN** um produto possui um grupo `HighestPrice` ou `Proportional` associado e uma composição válida é selecionada
- **THEN** o resultado do grupo define o preço-base composto da unidade; sem grupo de composição, permanece o preço da variação selecionada ou o preço-base do produto

#### Scenario: Grupo existente mantém preço
- **WHEN** um grupo de adicionais existente é migrado
- **THEN** sua estratégia passa a `Additive` e o preço do produto/variação continua sendo somado aos adicionais selecionados

#### Scenario: Opção exige escolha em outro grupo
- **WHEN** uma opção selecionada possui regra `Requires` para opção de outro grupo e a opção-alvo não foi selecionada
- **THEN** o sistema rejeita a composição

#### Scenario: Opções mutuamente incompatíveis
- **WHEN** duas opções ligadas por `Excludes` são selecionadas juntas
- **THEN** o sistema rejeita a composição, mesmo que a regra tenha sido cadastrada em apenas uma das opções

### Requirement: Classificação publicada acompanha produto público
O catálogo público SHALL apresentar média e contagem de avaliações publicadas por produto da unidade; produto sem avaliação SHALL omitir ambos.

#### Scenario: Produto avaliado
- **WHEN** produto possui avaliações publicadas
- **THEN** catálogo retorna sua média e contagem calculadas dentro da unidade

#### Scenario: Produto sem avaliação publicada
- **WHEN** produto não tem avaliações publicadas
- **THEN** catálogo não apresenta nota zero nem contagem como se houvesse avaliações
