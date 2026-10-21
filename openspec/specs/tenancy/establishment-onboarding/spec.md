# Establishment Onboarding Specification

## Purpose

Guia usuários autorizados pela configuração mínima e retomável necessária para colocar uma unidade do zFome em operação com segurança.

## Requirements

### Requirement: Onboarding possui etapas persistidas e retomáveis
O sistema SHALL registrar o progresso da unidade em etapas de dados, tema, horários, mesas e acesso, permitindo retomar sem recriar recursos concluídos.

#### Scenario: Navegador fechado durante configuração
- **WHEN** o administrador retorna ao onboarding de uma unidade parcialmente configurada
- **THEN** o sistema apresenta o progresso persistido e permite continuar da etapa pendente

### Requirement: Somente usuário autorizado configura a unidade
O sistema MUST validar Tenant, associação e capacidade administrativa em toda leitura ou alteração do onboarding.

#### Scenario: Unidade de outro Tenant
- **WHEN** um usuário tenta acessar o onboarding de unidade fora de seu Tenant
- **THEN** o sistema rejeita sem expor progresso ou configuração da unidade

### Requirement: Conclusão depende de prontidão mínima
O sistema SHALL concluir o onboarding somente quando dados obrigatórios, pelo menos um horário de atendimento válido e pelo menos um administrador ativo associado estiverem configurados.

#### Scenario: Horário ausente
- **WHEN** o administrador tenta concluir sem horário válido
- **THEN** o sistema mantém o onboarding pendente e identifica a etapa necessária

### Requirement: Repetição de etapa é idempotente
O sistema MUST evitar duplicação quando a mesma intenção de salvar uma etapa for repetida após falha de comunicação.

#### Scenario: Resposta perdida ao criar mesa
- **WHEN** o salvamento é processado e o cliente repete a mesma intenção
- **THEN** o sistema retorna o recurso original sem criar outra mesa

### Requirement: Progresso não substitui validação de domínio
O estado visual do onboarding MUST derivar de configurações válidas e não pode marcar como pronta uma unidade cujos recursos violem regras vigentes.

#### Scenario: Recurso necessário é posteriormente desativado
- **WHEN** uma configuração essencial deixa de satisfazer a prontidão
- **THEN** o sistema recalcula a situação operacional sem apagar o histórico do onboarding
