# Echo-Server

API ASP.NET Core 9 para **Echo**, foro anónimo localizado (radio 15 km).

## Requisitos

- .NET 9 SDK
- PostgreSQL 16 + **PostGIS** (recomendado vía Docker)

## Arranque rápido (Docker)

```bash
docker compose up --build
```

API: `http://localhost:8080`  
Swagger (Development): `http://localhost:8080/swagger`  
Health: `http://localhost:8080/health`

## Arranque local sin Docker Compose completo

1. Levanta solo la base:

```bash
docker compose up db -d
```

2. Ejecuta la API:

```bash
dotnet run --project src/Echo.Api
```

Por defecto la connection string es `Host=localhost;Port=5432;Database=echo;Username=echo;Password=echo`.

En Development, `Geo:SkipIpCheck` está en `true` (IPs locales no tienen GeoIP fiable). En producción ponlo a `false`.

## Endpoints

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/session/bootstrap` | No | Crea/recupera identidad anónima → JWT |
| GET | `/api/feed?lat=&lon=&cursor=` | JWT | Posts en 15 km |
| POST | `/api/posts` | JWT | Crear post `{ body, lat, lon, mediaId? }` |
| GET | `/api/posts/{id}?lat=&lon=` | JWT | Detalle + replies (si estás en rango) |
| POST | `/api/posts/{id}/replies` | JWT | Responder |
| POST | `/api/media` | JWT | Upload multipart `file` |
| PUT | `/api/me/avatar` | JWT | `{ mediaId }` |

## Despliegue en IIS (servidor `83.54.52.239`)

Sí: hay que publicar la API y configurar IIS (no basta con dejar solo el `dotnet run` local).

1. En el servidor Windows, instala el **ASP.NET Core 9 Hosting Bundle**.
2. Publica:

```bash
dotnet publish src/Echo.Api -c Release -o C:\inetpub\echo-api
```

3. En IIS: sitio nuevo → ruta física `C:\inetpub\echo-api` → binding HTTP al puerto **80** (o el que uses) en `83.54.52.239`.
4. Application Pool: **No Managed Code**, identidad con permiso de escritura en `App_Data\media` (o la carpeta de media).
5. Firewall de Windows / router: abrir el puerto del binding hacia Internet.
6. PostgreSQL+PostGIS debe estar accesible desde esa máquina; connection string en `appsettings.Production.json` o variables de entorno.
7. `ASPNETCORE_ENVIRONMENT=Production` en el Application Pool / web.config.
8. Comprueba `http://83.54.52.239/health`.

Si usas otro puerto (p. ej. 8080), la app Flutter debe usar `http://83.54.52.239:8080` y `Media:PublicBaseUrl` debe coincidir.

HTTPS (recomendado): certificado en el binding 443 y URLs `https://...`.

