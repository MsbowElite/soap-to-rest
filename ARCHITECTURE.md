# Arquitetura e Decisões Técnicas

Resumo:
- Dois serviços em contêineres: `csharp-soap` (provedor SOAP simulado) e `csharp-rest` (Minimal API acting as adapter).

Motivações e prioridades:
- Simular cenário legado onde um provedor SOAP fornece dados e um adapter REST torna a integração mais simples para clientes modernos.
- Aplicar princípios SOLID: dependências injetáveis (`IBiometriaRepository`), responsabilidades únicas (serviço SOAP apenas responde; adapter apenas orquestra), aberto/fechado (extensível com novas implementações de repositório).

Design e componentes:
- `csharp-soap`: expõe contrato SOAP via SoapCore. Simples, sem persistência.
- `csharp-rest`: Minimal API que:
  - usa `HttpClient` com `Polly` para retry/backoff (resiliência AO cliente SOAP).
  - registra `IBiometriaRepository` para separar persistência (atualmente `OracleMockRepository`).
  - usa `Serilog` para logging estruturado.
  - expõe Swagger para documentação OpenAPI.

Retry Policy:
- Implementado com `Polly` usando `WaitAndRetryAsync` exponencial com 3 tentativas.
- Motivo: tratar falhas transitórias, 429 e erros temporários de rede.

Observability:
- Logs estruturados via `Serilog` (console). Recomenda-se enviar para Elastic/Seq para produção.
- Endpoints de health e Swagger expostos.
- Próximo passo: integrar OpenTelemetry para tracing + Prometheus para métricas.

Migração para microsserviços plenos (estratégia):
1. Extrair clientes SOAP e contratos em bibliotecas compartilhadas.
2. Introduzir circuit-breaker e bulkheads (Resilience4j/Polly avançado).
3. Substituir `OracleMockRepository` por camada de acesso a dados com EFCore + provider Oracle.
4. Adotar CI/CD com pipelines, criação de imagens e promoção entre ambientes.

Como executar:

```bash
docker-compose up --build
```
