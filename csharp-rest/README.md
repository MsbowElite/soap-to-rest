# REST Adapter (C# Minimal API)

Endpoint: `/api/v1/biometria/{cpf}`

This Minimal API acts as an adapter consuming the SOAP service and returning data to external clients. It uses `Polly` for retry policy and exposes Swagger UI.

Run with docker-compose:

```bash
docker-compose up --build
```
