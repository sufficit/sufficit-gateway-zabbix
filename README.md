# Sufficit.Gateway.Zabbix

Pacote .NET do gateway Zabbix da Sufficit.

O projeto concentra:

- contratos públicos das integrações, destinos, execuções e automação;
- persistência Entity Framework do gateway;
- orquestração dos alertas telefônicos;
- configuração remota do Zabbix por JSON-RPC;
- extensões de injeção de dependências.

Os projetos consumidores usam o projeto local quando ele está disponível e o
pacote `Sufficit.Gateway.Zabbix` em instalações publicadas.

O target `netstandard2.0` contém somente contratos e interfaces para clientes
legados. Persistência Entity Framework, registro de serviços e automação remota
ficam nos targets modernos (`net7.0`, `net9.0` e `net10.0`). Essa separação evita
carregar o provedor MySQL legado e sua dependência Newtonsoft.Json nos clientes.

## Registro

```csharp
services.AddSufficitGatewayZabbix(configuration, loggerFactory);
```

O host precisa informar o callback público no `appsettings.json`:

```json
{
  "Sufficit": {
    "Gateway": {
      "Zabbix": {
        "PublicAlertEndpoint": "https://endpoints.example.com/gateway/zabbix/alert"
      }
    }
  }
}
```

`PublicAlertEndpoint` é obrigatório, deve ser uma URL HTTPS absoluta e não pode
conter query string ou fragmento. O pacote acrescenta `contextId` e `id` ao
configurar o webhook. Não existe endereço padrão ou fallback compilado no serviço.

O host deve registrar implementações para:

- `IZabbixTelephonyBridge`, conectando o gateway ao Call Dispatch;
- `IZabbixTokenProtector`, protegendo o token da API do cliente.

Essa configuração pertence exclusivamente ao host da API que recebe os alertas.
O runtime calcula `alert_callback_url` e a inclui nas integrações retornadas aos
clientes. Aplicações Blazor e outros clientes não devem registrar
`ZabbixGatewayOptions` nem duplicar `PublicAlertEndpoint` em seus appsettings.

## Banco de dados

Os scripts de referência ficam em `src/EntityFramework/Schema`.

## Contratos

Os modelos públicos em `src/Contracts` possuem documentação XML em classes,
enums, constantes e propriedades. A documentação gerada é incluída no pacote
para aparecer no IntelliSense dos projetos consumidores.

## Mensagens e localização

Toda comunicação operacional do pacote usa uma mensagem técnica em inglês e um
código estável no formato `SGZ9999`, definido em
`ZabbixGatewayMessageCodes`. Os consumidores devem localizar pelo código e usar
o texto inglês somente como fallback técnico.

Resultados bem-sucedidos expõem `message_code`. Falhas persistidas em execuções
e tentativas expõem `error_code`. Os endpoints ASP.NET Core retornam RFC 7807
com o código no campo `code`:

```json
{
  "type": "urn:sufficit:gateway:zabbix:error:SGZ2003",
  "title": "The Zabbix gateway request failed.",
  "status": 400,
  "detail": "A Zabbix API token is required.",
  "code": "SGZ2003",
  "error_kind": "Validation",
  "trace_id": "..."
}
```

Nunca use o texto de `detail` como chave de tradução. Os códigos existentes não
devem ser renumerados nem reutilizados para outro significado.

O script `202607241430-gatw-zabbix-error-codes.sql` adiciona as colunas que
persistem os códigos de erro de execuções e tentativas.
