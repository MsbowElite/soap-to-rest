# Copilot Instructions for soap-to-rest Repository

## Build & Test Commands

### Build
```bash
dotnet build
```

### Run Single Test
```bash
dotnet test --filter "SoapParserTests" --logger "console;verbosity=detailed"
```

### Run All Tests
```bash
dotnet test
```

### Run Locally with Docker
```bash
docker-compose up --build
```

### Run Individual Services (without Docker)
- **REST service**: `dotnet run --project csharp-rest`
- **SOAP service**: `dotnet run --project csharp-soap`

### Access Endpoints When Running
- REST API: `http://localhost:8080/api/v1/biometria/{cpf}`
- REST Health: `http://localhost:8080/health`
- Swagger UI: `http://localhost:8080/swagger/index.html`
- SOAP WSDL: `http://localhost:8081/soap/biometria.svc`

## High-Level Architecture

This is a proof-of-concept demonstrating SOAP-to-REST transformation:

### Two Services
1. **csharp-soap** (port 8081): Simulated legacy SOAP service using SoapCore that exposes a `Biometria` contract
2. **csharp-rest** (port 8080): Minimal API that acts as an adapter, consuming the SOAP service and exposing a REST endpoint

### csharp-rest Layers
- **Application Layer**: Business logic (`BiometriaService`), DTOs, validators (FluentValidation), and interfaces
- **Domain Layer**: Data models and value objects (currently minimal)
- **Infrastructure Layer**: 
  - SOAP client (`SoapBiometriaClient`) for HTTP calls to SOAP service
  - Repository pattern (`IBiometriaRepository`, `OracleMockRepository`)
  - Mappers for domain-to-DTO transformations
  - Middleware for cross-cutting concerns (correlation IDs)
  - Logging abstractions

### Resilience & Observability
- **Polly retry policy**: Exponential backoff (3 retries) on transient failures and 429 status codes
- **Serilog structured logging**: Enriched with correlation IDs, request/response logging via middleware
- **Health endpoint**: `/health` returns `{"status":"UP"}`

## Key Conventions

### Dependency Injection Pattern
- Register services in `Program.cs` under comments indicating layer (e.g., `// register layers: repository, mapper...`)
- Interfaces first: Define contracts in `Application/Interfaces`, implement in `Infrastructure`
- Use `AddScoped` for request-scoped services (e.g., SOAP client), `AddSingleton` for stateless services

### Request Validation
- Use FluentValidation validators registered in DI
- Validators inherit from `AbstractValidator<T>`
- Validate early in endpoint handlers using the injected validator
- Return 400 with structured error format on validation failure

### Naming Conventions
- **Interfaces**: `I{FeatureName}` (e.g., `IBiometriaRepository`)
- **Implementations**: `{FeatureName}{Type}` (e.g., `OracleMockRepository`, `SoapBiometriaClient`)
- **Constants**: Grouped in static classes under `Constants/` (e.g., `PolicyConstants`, `SoapConstants`)
- **DTOs**: In `Domain/Dto/` for output types, `Models/` for request/app models

### Result Pattern
- Use `Result<T>` for operation outcomes with `IsSuccess`, `IsNotFound`, `Value`, and `ErrorMessage` properties
- Supports early returns: `if (result.IsNotFound) return Results.NotFound(...)`

### Configuration
- SOAP base URL via environment variable `SOAP_BASE_URL` (defaults to localhost in config keys)
- Access via `builder.Configuration[ConfigKeys.SoapBaseUrl]`

### Response Format
Endpoint responses include `correlationId` for distributed tracing:
```json
{
  "correlationId": "uuid-string",
  "cpf": "12345678901",
  "status": "OK",
  "matchScore": 95.5
}
```

### Middleware & Cross-Cutting Concerns
- `CorrelationIdMiddleware`: Generates/extracts correlation IDs, stores in `HttpContext.Items`
- Available in handlers via `httpContext.Items[Headers.CorrelationId]`
- Logged via `LogContext.PushProperty` for structured logging enrichment

### Error Handling
- Validation errors: 400 Bad Request with error details
- Not found: 404 with `message` field
- Errors: 500 Problem Details with `CorrelationId` extension
- Log exceptions with context (CPF, correlation ID)

## Technology Stack
- **.NET 10.0** with implicit usings and nullable reference types enabled
- **Minimal APIs**: No traditional controller-based routing
- **Serilog + AspNetCore**: Structured logging with middleware integration
- **Polly**: HTTP resilience policies
- **FluentValidation**: Composable, type-safe validation
- **Swashbuckle**: OpenAPI/Swagger generation
- **xUnit**: Testing framework
- **Docker Compose**: Local orchestration of both services
