# CDN-backend

API REST en **C# / .NET 10 + SQL Server** para almacenar usuarios, imágenes (metadatos + CDN) y sus URLs.
El frontend es una app **React** que vive en el mismo repo (`CDN-client/`, ignorada por git).

📄 Documento de diseño y estado: [ESTRUCTURA.md](ESTRUCTURA.md)

## Stack

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core Web API (net10.0) |
| Base de datos | SQL Server (LocalDB en dev) |
| Acceso a datos | Dapper (SQL a mano) |
| Contraseñas | Argon2id (`Konscious.Security.Cryptography`) |
| Auth | JWT Bearer (claims: userId, username, nickname, role) |
| Imágenes | `IImageStorage` — disco local (dev) / Azure Blob (pendiente) |

## Requisitos

- .NET 10 SDK (o VS 2022+)
- SQL Server LocalDB (viene con VS)
- Node 20+ (solo para el cliente)

## Puesta en marcha

1. **Crear la base** (una vez):
   ```bash
   sqlcmd -S "(localdb)\MSSQLLocalDB" -i scripts/schema.sql
   ```
   O abrir `scripts/schema.sql` en VS (SQL Server Object Explorer) y ejecutarlo.

2. **Levantar la API:** abrir `CDN-backend.slnx` y presionar **F5** (perfil `https`, puerto 7057).

3. **Probar endpoints:** con el archivo `API/CDNBackend.API.http` (Send Request en VS) o con el cliente React.

4. **Levantar el cliente:**
   ```bash
   cd CDN-client
   npm install
   npm run dev        # → http://localhost:5173
   ```

## Configuración (`API/appsettings.json`)

| Clave | Qué es |
|---|---|
| `ConnectionStrings:Default` | Conexión a SQL Server (LocalDB en dev) |
| `Jwt:Key` | Clave de firma del JWT — **cambiar en producción** (user-secrets/env) |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | Emisor, audiencia y expiración del token |
| `Storage:Provider` | `Local` (disco, dev) o `Azure` (pendiente de implementar) |
| `Cors:AllowedOrigins` | Orígenes permitidos (por defecto `http://localhost:5173`) |
| `ImageUpload:MaxSizeBytes` / `AllowedContentTypes` | Tamaño máximo y tipos de imagen aceptados |

El log de requests en desarrollo se activa en `API/appsettings.Development.json` (`Microsoft.AspNetCore.Hosting: Information`).

## Endpoints

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | — | Crea usuario + credenciales → JWT |
| POST | `/api/auth/login` | — | Valida email + Argon2 → JWT |
| GET | `/api/users` | — | Lista usuarios (tabla pública) |
| GET | `/api/users/{id}` | — | Perfil por id |
| GET | `/api/users/{username}` | — | Perfil por username |
| PUT | `/api/users/{id}` | JWT | Edita nickname/username/descripción (dueño o admin) |
| POST | `/api/images` | JWT | Sube imagen (multipart) → CDN + metadata |
| GET | `/api/images` | — | Lista imágenes (`?userId=` para filtrar) |
| GET | `/api/images/{id}` | — | Metadata |
| GET | `/api/images/{id}/download` | — | Ver (`?download=true` para descargar) |
| DELETE | `/api/images/{id}` | JWT | Borra (dueño o admin) |
| GET | `/api/saved` | JWT | Imágenes guardadas del usuario actual |
| POST | `/api/saved/{imageId}` | JWT | Guarda una imagen (idempotente) |
| DELETE | `/api/saved/{imageId}` | JWT | Quita una imagen de los guardados |
| GET | `/api/admin/users` | admin | Lista usuarios con email (solo rol `admin`) |

## Roles y permisos

- Columna `Role` en `Users`: `user` (por defecto) o `admin`.
- El rol viaja en el JWT (claim `role`) y se controla con `[Authorize(Roles = "admin")]`.
- **Promover a admin** (no hay endpoint a propósito):
  ```sql
  UPDATE dbo.Users SET Role = 'admin' WHERE Username = 'tu_usuario';
  ```
- Los admins pueden: ver `GET /api/admin/users` y borrar cualquier imagen.

## Consultas SQL

Todas las consultas que ejecuta la API están documentadas en [scripts/consultas.sql](scripts/consultas.sql) — se pueden probar en VS reemplazando los `@parámetros` por valores.

## Estructura del proyecto

```
API/                proyecto web (Controllers, Models, Data, Services, Storage, Middleware)
scripts/            schema.sql (DDL) + consultas.sql (referencia de queries)
CDN-backend.slnx    solución
ESTRUCTURA.md       plan y estado del proyecto
```
