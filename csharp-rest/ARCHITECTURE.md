**REST Project Architecture**

Layers
- Controller: minimal API endpoints in `Program.cs` — thin HTTP adapters.
- Service (Application): `BiometriaService` implements business logic, retry + fallback policies, and orchestration between HttpClient, mapper and repository.
- Infrastructure: `OracleMockRepository` (persistence) and `BiometriaMapper` (mapping from SOAP XML to `BiometriaDto`).

Patterns & Decisions
- DI: All dependencies registered in `Program.cs` using built-in DI container.
- Mapper: Constructor-based immutable DTO (`BiometriaDto`) to make mapping resilient when fields are missing.
- Resilience: Polly retry + fallback policies applied in service layer; high-density logs added to capture retries, fallbacks and errors.
- SOLID: Interfaces separate concerns (service, mapper, repository). Controller is thin and depends on abstractions.

Mapping robustness
- `BiometriaMapper` uses namespace-agnostic XML lookup (`local-name()`) and returns safe defaults if elements are not present.

How to run
```
dotnet build Biometria.sln
docker-compose up --build
```
