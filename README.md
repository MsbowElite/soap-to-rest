# Prova de Conceito: Biometria (C# SOAP + REST Adapter)

Este repositório contém uma prova de conceito com dois serviços em .NET 10:

- `csharp-soap`: serviço SOAP simulado (SoapCore) que expõe `/soap/biometria.svc`.
- `csharp-rest`: Minimal API REST que age como adapter, consumindo o SOAP e expondo `/api/v1/biometria/{cpf}` para clientes externos.

Principais características implementadas:
- Arquitetura orientada a responsabilidades (SOLID): separação de contrato (`IBiometriaRepository`) e implementação (`OracleMockRepository`).
- Retry policy com `Polly` para chamadas HTTP ao serviço SOAP.
- Logging estruturado com `Serilog`.
- Documentação OpenAPI/Swagger para a Minimal API.
- Execução com `docker-compose` para facilitar testes locais.

Como executar:

```bash
docker-compose up --build
```

Endpoints:
- SOAP: http://localhost:8081/soap/biometria.svc (WSDL disponível via SoapCore conventions)
- REST: http://localhost:8080/api/v1/biometria/{cpf}
- Swagger UI (REST): http://localhost:8080/swagger/index.html

Observability e próximos passos:
- Logs estruturados via Serilog (console). Para produção, enviar para Elastic/Seq.
- Métricas e tracing podem ser adicionados via OpenTelemetry + Prometheus.
- Estratégia de migração: transformar o adapter em conjunto de microserviços, extrair cliente SOAP como uma biblioteca compartilhada, usar circuit-breaker e sidecars.

Leia os READMEs em `csharp-soap` e `csharp-rest` para detalhes de cada serviço.
