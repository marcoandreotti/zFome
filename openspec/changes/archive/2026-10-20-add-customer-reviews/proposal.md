# Proposal

## Why

Clientes que concluem pedidos não têm como registrar a qualidade da experiência nem ajudam outros visitantes a escolher produtos e estabelecimentos. Avaliações verificadas por pedido permitem construir essa confiança com notas claras e classificação calculada a partir de registros publicados.

## What Changes

- Adicionar avaliação pública do estabelecimento e dos produtos comprados, vinculada a pedido concluído e restrita à unidade resolvida pelo pedido.
- Aceitar notas inteiras de 1 a 5; exigir comentário normalizado de 10 a 1000 caracteres para notas 1 e 2.
- Permitir uma avaliação por pedido e alvo, com publicação idempotente, janela de 30 dias após conclusão e sem edição após publicação.
- Exibir autor como anônimo e calcular média, contagem e distribuição somente com avaliações publicadas; exibir a média desde a primeira avaliação.
- Permitir à equipe administrativa reprovar avaliações pendentes e ocultar/restaurar avaliações publicadas com motivo, mantendo histórico de moderação e sem alterar nota ou excluir o registro.
- Manter toda avaliação enviada como pendente, fora das consultas e médias públicas, até aprovação da equipe administrativa; reprovação é terminal e exige motivo, enquanto somente conteúdo publicado e ocultado pode ser restaurado.
- Apresentar convite de avaliação no acompanhamento do pedido concluído, classificação pública da unidade e dos produtos e uma superfície administrativa de moderação.
- Usar `QRating` (`q-rating`) do Quasar para entrada e exibição de notas, com rótulo textual e acessibilidade por teclado.

## Capabilities

### New Capabilities

- `reviews/customer-reviews`: criação, idempotência, elegibilidade, moderação e classificação derivada das avaliações.

### Modified Capabilities

- `catalog/product-catalog`: retornar média e contagem publicadas por produto no catálogo público, sem consultas N+1.
- `ordering/public-ordering-web`: convite para avaliar após conclusão e apresentação de classificação na experiência pública.

## Impact

- Novos módulos de domínio, aplicação, contratos e endpoints para reviews; escrita e histórico em schema próprio `reviews`, com sumário síncrono e isolamento por tenant/unidade.
- Novos endpoints públicos para consultar estado de avaliação do pedido, publicar avaliações e consultar classificação; endpoints administrativos para listar, aprovar/reprovar pendentes e ocultar/restaurar publicadas.
- Atualizações aos read models públicos de produto e unidade e às experiências públicas e administrativas Vue/Quasar.
- Nova migration aditiva e testes de domínio, aplicação, persistência, API e frontend; sem novas dependências planejadas.

