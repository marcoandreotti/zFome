# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

O usuário primário é o proprietário ou gestor do estabelecimento. Ele configura a unidade e o catálogo, disponibiliza as vendas públicas, recebe pedidos, organiza a operação e acompanha o negócio.

Outros usuários essenciais atuam em superfícies específicas: clientes que consultam ofertas e fazem pedidos; equipes de atendimento, cozinha e entrega que processam pedidos; e o superusuário da plataforma, que administra estabelecimentos com acesso global às unidades.

## Product Purpose

O zFome é uma plataforma SaaS multi-tenant para estabelecimentos de alimentação. Seu propósito é permitir que o proprietário ou gestor administre digitalmente o ciclo completo de pedidos, da configuração do catálogo e recebimento do pedido à preparação, acompanhamento e conclusão.

## Positioning

O zFome conecta em uma mesma plataforma a administração do estabelecimento, o canal público de pedidos e a operação de atendimento e cozinha, mantendo as responsabilidades e os acessos próprios de cada superfície.

## Operating Context

O produto é usado por estabelecimentos como bares, restaurantes e pizzarias. Proprietários e gestores trabalham na administração da unidade; clientes acessam ofertas e fazem pedidos pela web pública; equipes operacionais acompanham, preparam e concluem pedidos; e o superusuário atua no contexto global da plataforma.

O frontend existente fica em `web/ZFome.Web`. O usuário confirmou que `PublicOrderingPage` é a página pública e que ainda não a testou; outras partes do projeto foram descritas como funcionais. A disponibilidade técnica de uma página não deve ser tratada como validação funcional feita pelo usuário.

## Capabilities and Constraints

- A solução é um monólito modular SaaS multi-tenant para pedidos de estabelecimentos de alimentação.
- O frontend é web, construído com Vue.js, Quasar e TypeScript. O backend usa .NET 10 e C#; PostgreSQL é o banco principal, EF Core atende escrita e Dapper atende leitura.
- As superfícies públicas, administrativas, operacionais, KDS e de plataforma têm responsabilidades distintas e não devem ser unificadas apenas por conveniência de layout.
- O tema personalizado do estabelecimento pertence à superfície pública. A marca pública não deve alterar a linguagem visual administrativa, operacional, KDS ou de plataforma.
- O Tenant deve vir do contexto autenticado ou de resolução segura equivalente; consultas e ações devem preservar isolamento entre Tenants. O superusuário da plataforma opera no contexto global, conforme autorização do servidor.
- Autorização da interface não substitui validação pela API. Clientes não escolhem o Tenant de autorização enviando um identificador próprio.
- O usuário quer estabelecer um padrão de cores para o frontend. A paleta e sua aplicação ainda são uma decisão de design a ser trabalhada; este registro não escolhe cores.

## Product Principles

- Preservar o ciclo completo do pedido, da configuração à conclusão operacional.
- Manter os dados e as ações isolados por Tenant e unidade autorizada.
- Dar a cada tipo de usuário a superfície adequada ao seu trabalho.
- Manter pedidos, preços e estados autoritativos no servidor.
- Tornar as operações acessíveis e compreensíveis em seus dispositivos de uso.
