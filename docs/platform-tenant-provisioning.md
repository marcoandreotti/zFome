# Provisionamento de Tenant pela plataforma

## Pontos reutilizados

O fluxo reutiliza os aggregates `Tenant`, `Establishment` e `AdministrativeUser`, o papel `Owner`, as associações de unidade, o hashing de senhas, as sessões administrativas, os dispatchers próprios, `ProblemDetails` e o onboarding existente. A implementação adiciona somente a intenção idempotente, casos de uso globais, adapters específicos e a interface de plataforma; não cria repository genérico nem duplica o contexto de Tenant.

## Primeiro acesso

1. Autentique o `PlatformSuperUser` com o código `PLATFORM`, senha definitiva e MFA.
2. A aplicação direciona a sessão global para `/platform`.
3. Use **Provisionar Tenant** e informe Tenant, primeira unidade e primeiro Owner.
4. Entregue a senha temporária ao Owner por canal seguro fora do ZFome.
5. O Owner entra com o código público do Tenant, conclui o MFA e troca obrigatoriamente a senha.
6. Após novo login, o Owner pode continuar o onboarding da unidade.

A senha temporária é recebida somente na criação, armazenada como hash e nunca aparece em respostas, consultas ou logs previstos pela aplicação.

## Idempotência e recuperação

A web mantém a mesma chave de intenção enquanto o resultado de uma tentativa for desconhecido. Repetir exatamente o mesmo envio retorna os recursos já criados; reutilizar a chave com conteúdo diferente retorna conflito. A ação **Verificar resultado do último envio** recupera uma intenção concluída pelo mesmo ator global.

## Unidade adicional

Na área de plataforma, expanda um Tenant e escolha **Adicionar unidade**. A interface consulta os Owners ativos do Tenant por uma unidade existente, exige um Owner responsável e cria a nova associação junto da unidade. A unidade inicia com onboarding pendente.

## Implantação e rollback

Aplicar a migration `PlatformTenantProvisioning` antes de liberar a nova interface. Ela adiciona `identity.administrative_user.password_change_required` com padrão `false` e a tabela `tenancy.platform_provisioning_intent`.

Para rollback de aplicação, remova primeiro a exposição da rota e da interface global. Preserve a migration enquanto houver Tenants provisionados; reverter a migration apaga somente as intenções e a marca de troca obrigatória, portanto deve ocorrer apenas em ambiente controlado e depois de confirmar que não existem Owners dependentes da senha temporária.
