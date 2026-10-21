# Spec Delta

## MODIFIED Requirements

### Requirement: A preferência permanece no navegador

A aplicação SHALL persistir a opção selecionada no navegador atual e restaurá-la em visitas e superfícies futuras. Uma escolha feita em um navegador MUST NOT alterar preferências de conta ou de outros dispositivos.

#### Scenario: Retornar à aplicação
- **WHEN** a pessoa abre novamente o zFome no mesmo navegador
- **THEN** a última opção selecionada sob a identidade `zFome` é restaurada antes da interação com a interface
