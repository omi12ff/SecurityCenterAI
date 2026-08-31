# Security Center AI

Backend inicial del MVP para un copiloto de ciberseguridad. Incluye registro,
login con JWT, perfil básico de usuario, dashboard protegido y PostgreSQL.

## Requisitos

- .NET SDK 8
- Docker Desktop

## Uso diario del backend

Trabaja con la aplicación de Docker Compose llamada `securitycenterai`. El
contenedor `securitycenterai-postgres-test` no forma parte del entorno diario.

La primera vez en cada equipo, restaura la herramienta de migraciones y crea
una clave JWT local fuera del repositorio:

```powershell
dotnet tool restore
dotnet user-secrets set "Jwt:Key" "scai-$(New-Guid)-$(New-Guid)" --project src/SecurityCenterAI.Api
```

Desde la raíz del repositorio:

```powershell
docker compose up -d postgres
dotnet ef database update --project src/SecurityCenterAI.Infrastructure --startup-project src/SecurityCenterAI.Api -- --environment Development
dotnet run --project src/SecurityCenterAI.Api --launch-profile http
```

La API queda en `http://localhost:5169` y Swagger en
`http://localhost:5169/swagger`.

Para detener el backend al terminar:

```powershell
docker compose stop postgres
```

No uses `docker compose down -v` salvo que quieras borrar definitivamente los
datos locales.

Los valores incluidos en `appsettings.Development.json` y `docker-compose.yml`
son exclusivamente para desarrollo local. PostgreSQL solo se publica en
`127.0.0.1:55432`, evitando el PostgreSQL instalado en Windows que usa el
puerto `5432`.

## Contrato inicial para el frontend

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/profile` (requiere `Authorization: Bearer <token>`)
- `GET /api/dashboard` (requiere `Authorization: Bearer <token>`)
- `GET /health/live`
- `GET /health/ready`

En producción son obligatorias las variables `ConnectionStrings__PostgreSQL`
y `Jwt__Key`. La clave JWT debe ser aleatoria y tener al menos 32 bytes; nunca
se debe reutilizar la clave local ni subir secretos al repositorio.

## Verificación

```powershell
dotnet test SecurityCenterAI.sln
```

## Flujo Git recomendado

- `main`: versiones estables.
- `develop`: integración.
- `feature/backend-auth`: trabajo de autenticación.
- `feature/frontend-auth`: pantallas e integración del frontend.
