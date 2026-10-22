# Spec Delta

## ADDED Requirements

### Requirement: Classificação publicada acompanha produto público
O catálogo público SHALL apresentar média e contagem de avaliações publicadas por produto da unidade; produto sem avaliação SHALL omitir ambos.

#### Scenario: Produto avaliado
- **WHEN** produto possui avaliações publicadas
- **THEN** catálogo retorna sua média e contagem calculadas dentro da unidade

#### Scenario: Produto sem avaliação publicada
- **WHEN** produto não tem avaliações publicadas
- **THEN** catálogo não apresenta nota zero nem contagem como se houvesse avaliações
