# zFome

## Vision

zFome é uma plataforma SaaS Multi-Tenant para gerenciamento de pedidos
de bares, restaurantes, pizzarias e estabelecimentos similares.

## Principais usuários

- Cliente
- Atendente
- Cozinha
- Entregador
- Gerente
- Administrador
- Proprietário

## Canais

### Cliente

Acesso público por:

- URL
- QR Code
- QR Code da mesa
- link externo

### Operação

Área autenticada destinada à operação diária.

### Administração

Área autenticada destinada ao gerenciamento do estabelecimento.

## Objetivos arquiteturais

Priorizar:

1. simplicidade;
2. manutenção;
3. testabilidade;
4. isolamento de domínio;
5. segurança Multi-Tenant;
6. performance de leitura;
7. evolução futura.

## Estratégia

Modular Monolith inicialmente.

Bounded Contexts podem futuramente ser extraídos somente quando houver
necessidade técnica ou operacional comprovada.

Não desenhar a aplicação como microservices antecipadamente.