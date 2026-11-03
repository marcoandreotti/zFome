# ADR-005 — Linguagem visual Solid Glass adaptada ao zFome

## Contexto

O frontend do zFome possui tokens semânticos, quatro superfícies oficiais e temas
claro/escuro, mas ainda utiliza principalmente superfícies opacas, bordas simples e
sombras independentes por página. A direção visual proposta pelo projeto Solid Glass
usa espessura, filetes de luz, bordas finas, sombra de contato e blur controlado para
dar materialidade sem sacrificar leitura.

O Solid Glass original é voltado a React, Tailwind e shadcn/ui. O zFome utiliza Vue,
Quasar e Sass, portanto sua folha de estilos não pode ser incorporada diretamente.

## Problema

Precisamos evoluir a identidade visual do produto sem criar uma dependência de outro
framework, sem unificar superfícies com finalidades diferentes e sem permitir que a
personalização pública do Tenant altere as áreas administrativas ou operacionais.

## Opções

1. Adicionar React, Tailwind e shadcn/ui para consumir o Solid Glass original.
2. Copiar o stylesheet original e aplicar seus seletores sobre o Quasar.
3. Portar os princípios materiais para os tokens semânticos e componentes já usados
   pelo zFome, com intensidade ajustada por superfície.

## Decisão

Adotar a opção 3. O zFome terá uma linguagem visual Solid Glass adaptada, implementada
com CSS/Sass, tokens semânticos e seletores nativos do Quasar. A materialidade será
mais expressiva em Administration e Platform, moderada em Public e contida em
Operations/KDS, onde leitura rápida, contraste e desempenho são prioritários.

O blur será progressivo e opcional por token. Conteúdo denso, tickets de cozinha,
tabelas e estados semânticos não dependerão de transparência para comunicar sua
informação. A preferência claro/escuro existente continuará sendo a autoridade de
tema.

## Consequências

- Não adicionamos React, Tailwind, shadcn/ui ou outra biblioteca visual.
- Tokens de materialidade passam a ser parte do Design System e devem ser usados por
  novas superfícies elevadas.
- Componentes Quasar continuam sendo a base estrutural e comportamental.
- A alteração é transversal e exige validação visual, de contraste, responsividade e
  desempenho nas quatro superfícies.
- O efeito pode ser reduzido ou desligado por superfície sem remover a semântica dos
  componentes.
