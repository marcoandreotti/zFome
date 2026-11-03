# Direção visual do zFome

## Solid Glass adaptado

O zFome usa uma interpretação contida de Solid Glass: interfaces sólidas à primeira
vista, com materialidade percebida nos detalhes próximos. A profundidade vem de
espessura, filete superior de luz, hairline, sombra de contato e elevação; não de
gradientes decorativos ou transparência intensa.

### Princípios

- A leitura vem antes do efeito.
- A borda e a sombra definem a superfície; o centro permanece calmo.
- Há três espessuras: painel, card e popover.
- Ações e estados mantêm as cores semânticas do zFome.
- O tema público do Tenant continua isolado no subtree Public.
- Operations e KDS priorizam contraste, velocidade e baixo custo de pintura.

### Aplicação por superfície

| Superfície | Tratamento |
| --- | --- |
| Public | Cards, carrinho, busca e overlays podem usar materialidade moderada; imagens e preços permanecem sólidos. |
| Administration | Aplicação principal: shell, cards, menus, formulários e painéis usam espessura e elevação para organizar o trabalho. |
| Operations | Header e painéis usam contraste e borda; blur é reduzido para preservar leitura em uso contínuo. |
| KDS | Tickets e colunas permanecem opacos; a materialidade aparece em contornos e estados, nunca como ruído. |
| Platform | Materialidade moderada para reforçar o contexto global sem competir com ações de provisionamento. |

### Movimento e acessibilidade

Transições devem ser curtas e discretas. Quando `prefers-reduced-motion` estiver ativo,
blur, elevação animada e transformações devem ser reduzidos. Foco visível, contraste,
texto de estado e alvos de toque continuam obrigatórios.
