# Guia rápido de uso do zFome

Este guia mostra onde começar e quais passos seguir nas jornadas que já estão disponíveis. As telas e ações exibidas podem variar conforme a unidade ativa, a disponibilidade configurada e as permissões da conta.

## Índice por objetivo e perfil

- [Fazer e acompanhar um pedido (consumidor)](#fazer-e-acompanhar-um-pedido-consumidor)
- [Preparar uma unidade para receber pedidos (estabelecimento)](#preparar-uma-unidade-para-receber-pedidos-estabelecimento)
- [Administrar a unidade (gerente ou pessoa autorizada)](#administrar-a-unidade-gerente-ou-pessoa-autorizada)
- [Atender pedidos (equipe de operação)](#atender-pedidos-equipe-de-operação)
- [Provisionar um estabelecimento (equipe da plataforma)](#provisionar-um-estabelecimento-equipe-da-plataforma)
- [Referências e limitações](#referências-e-limitações)

## Fazer e acompanhar um pedido (consumidor)

**Onde começar:** abra o link público compartilhado pelo estabelecimento (`/order/{slug}`) ou leia o QR Code de uma mesa (`/order/{slug}/table/{token}`). O estabelecimento fornece o link ou o QR Code.

1. **Escolha no cardápio.** Navegue por categorias ou pesquise um produto. Se houver variações ou adicionais obrigatórios, faça as escolhas solicitadas antes de adicionar.
   - *Resultado:* o produto escolhido aparece no carrinho.
   - *Para que serve:* registrar os itens e a composição desejada.
2. **Revise o carrinho.** Confira os itens, escolha retirada ou entrega (ou use a mesa quando o acesso veio de um QR Code) e avance. O sistema recalcula valores, descontos e taxas com os dados atuais do estabelecimento.
   - *Resultado:* um resumo atualizado para seguir ao checkout.
   - *Para que serve:* revisar a intenção do pedido antes de informar os dados finais.
3. **Preencha o checkout.** Informe os dados solicitados para a modalidade escolhida. Em entrega, informe o endereço; se o estabelecimento oferecer agendamento, escolha um horário disponível. Selecione uma forma de pagamento ativa e, se desejar, informe um cupom.
   - *Resultado:* o pedido fica pronto para confirmação quando os campos e a disponibilidade forem válidos.
   - *Para que serve:* indicar como o pedido será atendido e fornecer o necessário para sua execução.
4. **Confirme e guarde o acompanhamento.** Após a confirmação, use **Acompanhar pedido** ou guarde o link apresentado. A rota de acompanhamento é `/order/track/{referência}`.
   - *Resultado:* estado atual e histórico do pedido; a tela atualiza as informações enquanto o pedido está em andamento.
   - *Para que serve:* acompanhar o andamento sem entrar na área administrativa.
5. **Cancele, se necessário e ainda permitido.** A tela de acompanhamento oferece cancelamento somente enquanto o pedido está confirmado. Depois que a equipe avança o pedido, essa ação deixa de estar disponível ao consumidor.

**Observação sobre e-mail:** o estabelecimento pode configurar notificações transacionais. A mensagem de confirmação ou de andamento só é solicitada quando há e-mail informado e um template ativo correspondente; o link de acompanhamento continua sendo a referência para consultar o estado.

## Preparar uma unidade para receber pedidos (estabelecimento)

**Pré-requisito:** uma conta administrativa autenticada e acesso à unidade. A pessoa responsável pela administração pode iniciar em **Configurar unidade** (`/administration/onboarding`).

1. **Dados:** informe nome e endereço público (slug). O slug compõe o link do cardápio.
2. **Tema:** personalize a apresentação pública, se desejar. Campos vazios usam o tema padrão.
3. **Horários:** configure os períodos de atendimento. Dias sem intervalos ficam fechados.
4. **Mesas e QR:** cadastre mesas e gere os QR Codes para pedidos associados a uma mesa. Se alterar o endereço público ou renovar um token, compartilhe/imprima os QR Codes atualizados.
5. **Acessos:** configure as contas e permissões da equipe conforme as opções disponíveis.
6. **Revisão:** confira a configuração e conclua o onboarding.

Salve cada etapa; é possível continuar depois. Tema e mesas são opcionais. Para começar a vender, a unidade também precisa de produtos e modalidades de atendimento disponíveis.

## Administrar a unidade (gerente ou pessoa autorizada)

**Onde começar:** entre pelo `/login` com a conta administrativa. Depois da autenticação, abra a área Administração. O menu lateral mostra somente as opções permitidas para a conta e mantém visível a unidade ativa.

| Objetivo | Caminho | Para que serve |
| --- | --- | --- |
| Ver resumo da unidade | Administração → Visão geral (`/administration`) | Consultar indicadores e atalhos administrativos disponíveis. |
| Manter produtos e cardápio | Administração → Catálogo (`/administration/catalog`) | Cadastrar e organizar produtos, variações e adicionais. |
| Configurar atendimento e horários | Administração → Disponibilidade (`/administration/availability`) | Ajustar modalidades, horários e opções de agendamento. |
| Configurar área atendida | Administração → Regiões de entrega (`/administration/delivery-regions`) | Definir regiões, custos e estimativas de entrega. |
| Gerenciar equipe | Administração → Usuários (`/administration/users`) | Administrar contas e permissões disponíveis. |
| Consultar clientes | Administração → Clientes (`/administration/customers`) | Localizar e manter registros de clientes. |
| Configurar promoções | Administração → Cupons (`/administration/coupons`) | Criar e manter cupons conforme as regras da unidade. |
| Configurar formas de pagamento | Administração → Formas de pagamento (`/administration/payment-methods`) | Definir as formas que podem ser escolhidas no pedido. |
| Configurar mensagens | Administração → Notificações (`/administration/communications`) | Gerenciar templates, consentimentos e histórico de envio. |

As opções dependem das capacidades atribuídas à conta. A configuração de uma forma de pagamento não significa que o zFome processe pagamento online; essa jornada depende do tipo de forma ativa na unidade.

## Atender pedidos (equipe de operação)

**Pré-requisito:** conta autenticada com acesso operacional à unidade. Entre pelo `/login` e abra Operações.

1. **Pedidos** (`/operations`): acompanhe a fila por estado, revise informações do pedido e execute a próxima ação permitida para seu papel.
2. **Cozinha** (`/operations/kitchen`): consulte a fila de preparo e inicie ou conclua o trabalho conforme as ações disponíveis.
3. **Entregas** (`/operations/delivery`): acompanhe os pedidos em andamento associados à entrega.

O painel atualiza a fila e informa quando os dados podem estar desatualizados. Se uma ação entrar em conflito com uma atualização feita por outra pessoa, confira o estado recarregado antes de continuar. Os links de navegação aparecem de acordo com as permissões da conta.

## Provisionar um estabelecimento (equipe da plataforma)

Este caminho é destinado à identidade de plataforma autorizada, não às contas comuns do estabelecimento. Acesse `/platform` após autenticar e consulte [Provisionamento de Tenant pela plataforma](platform-tenant-provisioning.md) para criar o Tenant, sua primeira unidade e a conta Owner. Depois, o Owner pode entrar na área administrativa e continuar o onboarding.

## Referências e limitações

- O cardápio público exige um slug ativo; pedidos em mesa usam também um token de QR válido.
- Modalidades, produtos, horários, slots de agendamento, cupons e formas de pagamento dependem da configuração e disponibilidade retornadas pelo servidor. Um item no carrinho não garante que ainda possa ser confirmado.
- O checkout usa as formas de pagamento habilitadas pela unidade. Esta documentação não descreve integração com provedor de pagamento online.
- O link de acompanhamento usa uma referência pública opaca; não compartilhe identificadores internos.
- Para detalhes de notificações, templates, consentimento e Mailpit, consulte [Notification Gateway](notification-gateway.md). Esse material é uma referência técnica para desenvolvimento local.
- Para os detalhes do primeiro acesso e provisionamento pela plataforma, consulte [Provisionamento de Tenant](platform-tenant-provisioning.md).

Os caminhos operacionais e administrativos acima são uma visão inicial. O guia não substitui instruções detalhadas de cada módulo nem documentação técnica da API.
