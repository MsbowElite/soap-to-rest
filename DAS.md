# Documentação de Arquitetura de Solução (DAS)
## Adaptador SOAP-to-REST para Serviço de Biometria

**Versão:** 1.0  
**Data:** Julho 2026  
**Status:** Ativo

---

## 1. Visão Geral da Solução

### 1.1 Objetivo
Desenvolver um adaptador que transforme requisições REST em chamadas SOAP para um serviço legado de biometria, expondo uma API moderna enquanto mantém compatibilidade com sistemas legados.

### 1.2 Contexto de Negócio
- **Problema:** Serviço legado de biometria utiliza apenas SOAP (SoapCore)
- **Solução:** Criar uma camada intermediária REST que consome SOAP internamente
- **Benefício:** Clientes modernos podem usar REST; sistema legado continua funcionando

### 1.3 Escopo
- ✅ Adaptação de protocolo (REST ↔ SOAP)
- ✅ Validação de CPF (com check digit)
- ✅ Mapeamento de respostas SOAP XML
- ✅ Persistência em banco de dados Oracle
- ✅ Resiliência e retry automático
- ✅ Logging estruturado com rastreamento correlacionado
- ✅ Health check e monitoramento
- ❌ Autenticação avançada (fora do escopo desta versão)
- ❌ Cache distribuído (v2+)

---

## 2. Arquitetura Geral

### 2.1 Diagrama de Componentes

```
┌─────────────────────────────────────────────────────────────────┐
│                        Cliente REST                              │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP/REST
                             ▼
        ┌────────────────────────────────────────┐
        │   Serviço REST (csharp-rest)           │
        │   Porta: 8080                          │
        ├────────────────────────────────────────┤
        │  ┌──────────────────────────────────┐  │
        │  │    Minimal API Endpoints         │  │
        │  │  GET /api/v1/biometria/{cpf}    │  │
        │  │  GET /health                     │  │
        │  │  GET /swagger                    │  │
        │  └──────────────────────────────────┘  │
        │           ▲                             │
        │           │                             │
        │  ┌────────┴──────────────────────────┐  │
        │  │   Camada de Aplicação             │  │
        │  ├──────────────────────────────────┤  │
        │  │ • BiometriaService               │  │
        │  │ • BiometriaRequestValidator      │  │
        │  │ • BiometriaMapper                │  │
        │  └────────────────────────────────────┘  │
        │                                          │
        │  ┌──────────────────────────────────┐  │
        │  │   Camada de Infraestrutura        │  │
        │  ├──────────────────────────────────┤  │
        │  │ • SoapBiometriaClient            │  │
        │  │ • OracleMockRepository           │  │
        │  │ • CorrelationIdMiddleware        │  │
        │  │ • Polly RetryPolicy              │  │
        │  │ • Serilog Logging                │  │
        │  └──────────────────────────────────┘  │
        └────────────────────────┬─────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                    ▼ HTTP/SOAP              ▼
        ┌─────────────────────────┐  ┌──────────────────┐
        │  Serviço SOAP (legado)  │  │  Banco Oracle    │
        │  Porta: 8081            │  │  Mock Repository │
        │  WSDL: /soap/...        │  │  (persistência)  │
        └─────────────────────────┘  └──────────────────┘
```

### 2.2 Fluxo de Requisição

```
1. Cliente HTTP
   │
   ├─→ GET /api/v1/biometria/12345678909
   │
2. Middleware (CorrelationIdMiddleware)
   ├─→ Extrai/Gera correlation-id
   ├─→ Armazena em HttpContext.Items
   │
3. Validação (FluentValidation)
   ├─→ CPF não vazio? ✓
   ├─→ CPF válido (check digit)? ✓
   │
4. Chamada ao Serviço
   ├─→ BiometriaService.GetBiometriaAsync()
   │
5. Cliente SOAP
   ├─→ HTTP POST para http://localhost:8081/soap/...
   ├─→ Polly Retry: 3 tentativas + backoff exponencial
   │
6. Mapeamento
   ├─→ BiometriaMapper.MapFromSoap()
   ├─→ Parse XML (escaped ou top-level)
   ├─→ Retorna BiometriaDto
   │
7. Persistência
   ├─→ OracleMockRepository.SaveBiometriaAsync()
   │
8. Resposta REST
   ├─→ StatusCode 200 + JSON com correlationId
   │
9. Logging (Serilog)
   ├─→ Estruturado com enriquecimento de contexto
```

---

## 3. Componentes da Solução

### 3.1 Camada de Apresentação

#### 3.1.1 Minimal APIs (Program.cs)

**Responsabilidade:** Definir rotas HTTP e orquestrar injeção de dependências

**Endpoints:**

| Verbo | Rota | Descrição | Status |
|-------|------|-----------|--------|
| GET | `/api/v1/biometria/{cpf}` | Busca dados biométricos | 200, 400, 404, 500 |
| GET | `/health` | Verificação de saúde | 200 |
| GET | `/swagger/index.html` | Documentação OpenAPI | 200 |

**Exemplo de Resposta (Sucesso):**
```json
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "cpf": "12345678909",
  "status": "OK",
  "matchScore": 95.5
}
```

**Exemplo de Resposta (Validação):**
```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "cpf": ["CPF inválido."]
  },
  "correlationId": "..."
}
```

### 3.2 Camada de Aplicação

#### 3.2.1 BiometriaService

**Responsabilidade:** Orquestrar a lógica de negócio

**Métodos Principais:**
- `GetBiometriaAsync(cpf: string): Task<Result<BiometriaDto>>`

**Fluxo:**
1. Validar entrada
2. Chamar SoapBiometriaClient
3. Mapear resposta com BiometriaMapper
4. Verificar status NOT_FOUND
5. Persistir em repositório
6. Retornar Result<T>

**Pattern: Result<T>**
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsNotFound { get; }
    public T Value { get; }
    public string ErrorMessage { get; }
}
```

#### 3.2.2 Validadores (FluentValidation)

**BiometriaRequestValidator:**
- CPF não pode ser vazio
- CPF deve ser válido (11 dígitos + check digit)

**CpfValidator (utilitário):**
```csharp
public static bool IsValid(string cpf)
{
    // 1. Validar 11 dígitos
    // 2. Rejeitar todos-iguais (ex: "11111111111")
    // 3. Calcular primeiro dígito verificador (módulo 11)
    // 4. Calcular segundo dígito verificador (módulo 11)
    // 5. Comparar com input
}
```

#### 3.2.3 Interfaces (Contrato de Dependências)

```csharp
// Repositório
public interface IBiometriaRepository
{
    Task SaveBiometriaAsync(BiometriaDto dto);
}

// Cliente SOAP
public interface ISoapBiometriaClient
{
    Task<string> GetBiometriaAsync(string cpf);
}

// Mapeador
public interface IBiometriaMapper
{
    BiometriaDto MapFromSoap(string soapContent, string cpf);
}

// Logger
public interface ILoggerService
{
    void LogInformation(string message);
    void LogError(string message, Exception ex);
}
```

### 3.3 Camada de Infraestrutura

#### 3.3.1 SoapBiometriaClient

**Responsabilidade:** Comunicação HTTP com serviço SOAP legado

**Características:**
- Integrado com Polly para retry automático
- Timeout configurável
- Logging de requisição/resposta
- Enriquecimento com correlation-id

**Polly Retry Policy:**
```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<OperationCanceledException>()
    .OrResult<HttpResponseMessage>(r => 
        r.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
        (int)r.StatusCode >= 500)
    .WaitAndRetry(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Backoff exponencial
        onRetry: (outcome, timespan, attempt, context) => { /* log */ }
    );
```

#### 3.3.2 BiometriaMapper

**Responsabilidade:** Converter resposta SOAP XML para DTO REST

**Lógica de Parsing:**
1. **Tenta encontrar conteúdo escapado:**
   ```xml
   <GetBiometriaResult>
     &lt;BiometriaResponse&gt;
       &lt;cpf&gt;12345678909&lt;/cpf&gt;
       &lt;status&gt;OK&lt;/status&gt;
       &lt;matchScore&gt;95.5&lt;/matchScore&gt;
     &lt;/BiometriaResponse&gt;
   </GetBiometriaResult>
   ```

2. **Se falhar, tenta elementos top-level:**
   ```xml
   <soap:Body>
     <status>OK</status>
     <matchScore>95.5</matchScore>
   </soap:Body>
   ```

3. **Se ainda falhar, retorna NOT_FOUND**

**Tratamento de Cultura:**
- Usa `CultureInfo.InvariantCulture` para parsing de decimais
- Garante compatibilidade com diferentes locales (pt-BR, en-US, etc.)

#### 3.3.3 OracleMockRepository

**Responsabilidade:** Persistência (mock para esta versão)

**Implementação Atual:**
- Armazena em dicionário em memória
- Simula delay de I/O (100ms)
- Preparado para integração com Oracle real

**Próximas Versões:**
- Integração com Oracle Connection Strings
- Entity Framework Core
- Migration scripts

#### 3.3.4 CorrelationIdMiddleware

**Responsabilidade:** Rastreamento distribuído de requisições

**Implementação:**
1. Verifica header `X-Correlation-Id` na requisição
2. Se ausente, gera novo GUID
3. Armazena em `HttpContext.Items[Headers.CorrelationId]`
4. Injeta em todas as respostas e logs

**Benefício:** Rastrear uma transação através de múltiplos serviços

---

## 4. Padrões de Design e Boas Práticas

### 4.1 Padrão: Injeção de Dependência (DI)

**Registro em Program.cs:**
```csharp
// Camada de Aplicação
builder.Services.AddScoped<IBiometriaService, BiometriaService>();
builder.Services.AddScoped<BiometriaRequestValidator>();
builder.Services.AddScoped<IBiometriaMapper, BiometriaMapper>();

// Camada de Infraestrutura
builder.Services.AddScoped<ISoapBiometriaClient, SoapBiometriaClient>();
builder.Services.AddScoped<IBiometriaRepository, OracleMockRepository>();
builder.Services.AddSingleton<ILoggerService, SerilogLoggerService>();

// Resiliência
builder.Services.AddHttpClient<ISoapBiometriaClient, SoapBiometriaClient>()
    .AddTransientHttpErrorPolicy();
```

**Benefícios:**
- Testabilidade (mock dependencies em testes)
- Flexibilidade (trocar implementação sem mudar código)
- Separação de responsabilidades

### 4.2 Padrão: Result<T> (Monadic Error Handling)

Alternativa a exceções para operações que podem falhar previsivelmente:

```csharp
var result = await _service.GetBiometriaAsync(cpf);

if (result.IsNotFound)
    return Results.NotFound(new { message = "Biometria não encontrada" });

if (!result.IsSuccess)
    return Results.BadRequest(result.ErrorMessage);

return Results.Ok(result.Value);
```

**Vantagens:**
- Tratamento explícito de erros
- Menos exceções, mais fluxo
- Type-safe

### 4.3 Padrão: Middleware Pipeline

Ordem crítica em Program.cs:
```
1. Logging
2. CorrelationIdMiddleware (antes de qualquer lógica)
3. Validation
4. Error Handling
5. Endpoints
```

### 4.4 Padrão: Versionamento de API

- Versão na rota: `/api/v1/biometria/{cpf}`
- Facilita evolução sem quebrar clientes antigos
- Plano para v2: cache, novas features

---

## 5. Tecnologias e Dependências

### 5.1 Stack Técnico

| Camada | Tecnologia | Versão | Propósito |
|--------|-----------|--------|----------|
| Runtime | .NET | 10.0 | Framework principal |
| Web | Minimal APIs | 10.0 | Endpoints REST |
| Validação | FluentValidation | 11.x | Regras de validação |
| HTTP/Cliente | HttpClient + Polly | 10.0 + 8.x | Chamadas SOAP resilientes |
| Logging | Serilog | 3.x | Logs estruturados |
| Testing | xUnit | 2.9.3 | Framework de testes |
| Mocking | Moq | 4.20.70 | Mock em testes |
| API Docs | Swashbuckle | 10.x | Swagger/OpenAPI |
| Containerização | Docker Compose | 3.8 | Orquestração local |

### 5.2 NuGet Packages (csharp-rest)

```xml
<ItemGroup>
    <PackageReference Include="FluentValidation" Version="11.x" />
    <PackageReference Include="Polly" Version="8.x" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.x" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.x" />
</ItemGroup>
```

### 5.3 Dependências de Teste (csharp-rest-tests)

```xml
<ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0" />
    <PackageReference Include="FluentValidation.TestHelper" Version="11.x" />
</ItemGroup>
```

---

## 6. Segurança e Confiabilidade

### 6.1 Segurança

| Aspecto | Mecanismo | Status |
|---------|-----------|--------|
| Validação de Entrada | FluentValidation + CPF check digit | ✅ Implementado |
| CORS | Configurável em Program.cs | ⚠️ Requer setup |
| HTTPS | Suportado por .NET 10 | ⚠️ Requer certificado |
| Autenticação | JWT/OAuth | ❌ v2+ |
| Rate Limiting | Middleware customizado | ❌ v2+ |
| Logs Sensíveis | Mascarar CPF em logs | ✅ Implementado |

### 6.2 Resiliência

| Padrão | Implementação | Benefício |
|--------|--------------|----------|
| Retry | Polly (3 tentativas, backoff exponencial) | Recupera de falhas transitórias |
| Timeout | Configurável (30s default) | Evita travamentos |
| Circuit Breaker | Polly integrado | Evita sobrecarga de serviço legado |
| Fallback | Result<T> com NotFound | Degradação graciosa |

### 6.3 Observabilidade

#### Logging Estruturado (Serilog)
```csharp
Log.Information("Biometria solicitada para CPF {CpfHash}, CorrelationId {CorrelationId}",
    cpfHash, correlationId);

Log.Error(ex, "Falha ao buscar biometria. CorrelationId {CorrelationId}", 
    correlationId);
```

#### Correlation IDs
- Associam logs de múltiplos serviços
- Rastreiam requisição end-to-end
- Inclsos em resposta HTTP para auditoria do cliente

#### Health Endpoint
```
GET /health → { "status": "UP" }
```

---

## 7. Processo de Build e Deployment

### 7.1 Build Local

```bash
# Restaurar dependências
dotnet restore

# Compilar
dotnet build

# Executar testes
dotnet test

# Publicar
dotnet publish -c Release
```

### 7.2 Docker Compose (Ambiente Local)

```yaml
version: '3.8'

services:
  soap-service:
    build: ./csharp-soap
    ports:
      - "8081:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development

  rest-service:
    build: ./csharp-rest
    ports:
      - "8080:8080"
    environment:
      SOAP_BASE_URL: "http://soap-service:8080"
      ASPNETCORE_ENVIRONMENT: Development
    depends_on:
      - soap-service
```

**Comando:**
```bash
docker-compose up --build
```

### 7.3 CI/CD Pipeline (Sugerido)

```
1. Trigger: Push para main/develop
2. Build: dotnet build
3. Testes: dotnet test com coverage
4. Análise: SonarQube (qualidade do código)
5. Docker: docker build + push
6. Deploy: kubectl apply (ou docker-compose)
7. Smoke Tests: Health check + requisições básicas
```

---

## 8. Estrutura de Diretórios

```
soap-to-rest/
├── csharp-rest/                  # Serviço REST principal
│   ├── Application/
│   │   ├── Constants/            # Constantes da aplicação
│   │   ├── Interfaces/           # Contratos (I*)
│   │   ├── Models/               # DTOs e modelos de entrada
│   │   ├── Services/             # Lógica de negócio
│   │   └── Validators/           # Validações FluentValidation
│   ├── Domain/
│   │   └── Dto/                  # Objetos de transferência
│   ├── Infrastructure/
│   │   ├── Clients/              # Clients HTTP/SOAP
│   │   ├── Logging/              # Serviços de logging
│   │   ├── Mappers/              # Conversores de DTO
│   │   ├── Middleware/           # Middleware customizado
│   │   └── Repositories/         # Data access
│   ├── Helpers/                  # Utilitários diversos
│   ├── Properties/               # Configurações de execução
│   ├── Program.cs                # Configuração principal
│   └── CsharpRest.csproj         # Arquivo de projeto
│
├── csharp-rest-tests/            # Testes xUnit
│   ├── BiometriaIntegrationTests.cs
│   ├── ValidatorTests.cs
│   ├── MapperTests.cs
│   ├── ServiceTests.cs
│   ├── SoapParserTests.cs
│   └── CsharpRest.Tests.csproj
│
├── csharp-soap/                  # Serviço SOAP legado
│   ├── Program.cs
│   └── CsharpSoap.csproj
│
├── docker-compose.yml            # Orquestração Docker
├── Dockerfile                    # Imagens container
├── ARCHITECTURE.md               # Documentação técnica
├── DAS.md                        # Este documento
├── README.md                     # Guia de uso
└── Biometria.sln                # Solução Visual Studio
```

---

## 9. Fluxos Principais

### 9.1 Fluxo Feliz (Happy Path)

```
Cliente: GET /api/v1/biometria/12345678909
         ↓
Servidor: Valida CPF ✓
         ↓
         Chama SOAP (com retry)
         ↓
         Parseia XML → BiometriaDto
         ↓
         Salva em repositório
         ↓
Resposta: HTTP 200 + JSON
```

### 9.2 Validação Falha

```
Cliente: GET /api/v1/biometria/00000000000
         ↓
Servidor: Valida CPF ✗ (todos iguais)
         ↓
Resposta: HTTP 400 + Erro
```

### 9.3 SOAP Falha

```
Cliente: GET /api/v1/biometria/12345678909
         ↓
Servidor: Valida CPF ✓
         ↓
         Tenta Chamada SOAP → Falha (ex: timeout)
         ↓
         Polly Retry: Tentativa 1/3 → Falha
         ↓
         Polly Retry: Tentativa 2/3 → Falha
         ↓
         Polly Retry: Tentativa 3/3 → Falha
         ↓
Resposta: HTTP 500 + Erro com CorrelationId
```

### 9.4 Biometria Não Encontrada

```
Cliente: GET /api/v1/biometria/99999999999
         ↓
Servidor: Valida CPF ✓
         ↓
         Chama SOAP → Status: "NOT_FOUND"
         ↓
         Service detecta NOT_FOUND
         ↓
Resposta: HTTP 404 + Mensagem
```

---

## 10. Monitoramento e Operações

### 10.1 Métricas Recomendadas

| Métrica | Origem | Alertas |
|---------|--------|---------|
| Taxa de Erro | Application Insights / Prometheus | > 5% |
| Latência P95 | APM | > 2s |
| Taxa de Retry | Logs | > 10% |
| Uptime SOAP | Health checks | < 99% |
| Espaço em Disco | SO | > 80% |

### 10.2 Logs Importantes

**Sucesso:**
```
[INF] Biometria obtida com sucesso. CPF: {hash}, Status: OK, MatchScore: 95.5, CorrelationId: {id}
```

**Erro:**
```
[ERR] Falha ao obter biometria. CPF: {hash}, Tentativas: 3, Exception: {...}, CorrelationId: {id}
```

### 10.3 Troubleshooting

| Problema | Causa Provável | Solução |
|----------|---|---|
| 400 Bad Request | CPF inválido | Validar formato (11 dígitos) |
| 404 Not Found | Biometria não existe | Verificar CPF no serviço SOAP |
| 500 Error | SOAP inacessível | Verificar connectivity/logs |
| Timeout | Serviço SOAP lento | Aumentar timeout ou escalar |

---

## 11. Considerações Futuras (v2+)

### 11.1 Melhorias Planejadas

- [ ] **Cache Distribuído:** Redis para resultados frequentes
- [ ] **Autenticação:** JWT/OAuth 2.0
- [ ] **Rate Limiting:** Proteção contra abuso
- [ ] **GraphQL:** Além de REST
- [ ] **Async Queuing:** RabbitMQ para requisições assincronas
- [ ] **Database Real:** Oracle/SqlServer vs mock
- [ ] **Observabilidade:** OpenTelemetry + Jaeger
- [ ] **API Gateway:** Kong/Ocelot para roteamento

### 11.2 Debt Técnico Conhecido

- ⚠️ OracleMockRepository é apenas simulado
- ⚠️ Sem autenticação/autorização
- ⚠️ Sem rate limiting
- ⚠️ Sem cache (requests redundantes)

---

## 12. Referências e Links

- **[.NET 10 Documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)**
- **[FluentValidation Guide](https://docs.fluentvalidation.net/)**
- **[Polly Patterns](https://github.com/App-vNext/Polly)**
- **[Serilog Enrichers](https://github.com/serilog/serilog/wiki)**
- **[SOAP vs REST Comparison](https://www.geeksforgeeks.org/soap-vs-rest-api-difference/)**

---

## Apêndice A: Configuração de Ambiente

### Variáveis de Ambiente

```bash
# REST Service
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:8080
SOAP_BASE_URL=http://localhost:8081

# SOAP Service
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:8081
```

### Connection Strings (Futuro)

```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=ORCL;User Id=biometria;Password=***;"
  }
}
```

---

## Apêndice B: Checklist de Deploy

- [ ] Testes locais passando (59/59)
- [ ] Build sem warnings
- [ ] Dockerfile build successfully
- [ ] Docker Compose up sem erros
- [ ] Smoke tests: GET /health → 200
- [ ] Smoke tests: GET /api/v1/biometria/{cpf} → 200/404
- [ ] Logs não contêm dados sensíveis
- [ ] CorrelationId presente em respostas
- [ ] Monitoring alerts configurados
- [ ] Documentação atualizada

---

**Documento Finalizado**  
Próxima Revisão: Dezembro 2026 ou após release v1.1

