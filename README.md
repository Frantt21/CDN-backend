# CDN-backend

API REST en **C# / .NET 10 + SQL Server** para almacenar usuarios, imágenes (metadatos + CDN) y sus URLs.
El frontend es una app **React** que vive en el mismo repo (`CDN-client/`, ignorada por git).

Documento de diseño y estado: [ESTRUCTURA.md](ESTRUCTURA.md)

## Stack

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core Web API (net10.0) |
| Base de datos | SQL Server (LocalDB en dev) |
| Acceso a datos | Dapper (SQL a mano) |
| Contraseñas | Argon2id (`Konscious.Security.Cryptography`) |
| Auth | JWT Bearer + refresh tokens con rotación |
| Tiempo real | SignalR (hub `/hubs/feed`) |
| Miniaturas | `SixLabors.ImageSharp` (max 800px al subir) |
| Imágenes | `IImageStorage` — disco local (dev) / Azure Blob (pendiente) |

## Requisitos

- .NET 10 SDK (o VS 2022+)
- SQL Server LocalDB (viene con VS)
- Node 20+ (solo para el cliente)

## Puesta en marcha

1. **Crear la base** (instalación limpia, borra lo existente):
   ```bash
   sqlcmd -S "(localdb)\MSSQLLocalDB" -i scripts/schema.sql
   ```
   O abrir `scripts/schema.sql` en VS (SQL Server Object Explorer) y ejecutarlo.

2. **Base existente sin tocar datos:** correr las migraciones idempotentes de `scripts/migrations/` (ver sección Scripts).

3. **Levantar la API:** abrir `CDN-backend.slnx` y presionar **F5** (perfil `https`, puerto 7057).

4. **Probar endpoints:** con el archivo `API/CDNBackend.API.http` (Send Request en VS) o con el cliente React.

5. **Levantar el cliente:**
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
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y audiencia del token |
| `Jwt:ExpiryMinutes` | Expiración del access token |
| `Jwt:RefreshTokenExpiryDays` | Vida del refresh token (por defecto 7 días) |
| `Storage:Provider` | `Local` (disco, dev) o `Azure` (pendiente de implementar) |
| `Cors:AllowedOrigins` | Orígenes permitidos (por defecto `http://localhost:5173`) |
| `ImageUpload:MaxSizeBytes` | Tamaño máximo de subida (10 MB por defecto) |
| `ImageUpload:AllowedContentTypes` | Tipos de imagen aceptados (jpg, png, gif, webp) |
| `ImageUpload:ThumbnailMaxSize` | Lado máximo de la miniatura en px (800 por defecto) |

El log de requests en desarrollo se activa en `API/appsettings.Development.json` (`Microsoft.AspNetCore.Hosting: Information`).

## Endpoints

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | — | Crea usuario + credenciales → JWT + refresh token |
| POST | `/api/auth/login` | — | Valida email + Argon2 → JWT + refresh token |
| POST | `/api/auth/refresh` | — | Renueva la sesión (rota el refresh token) |
| GET | `/api/users` | — | Lista usuarios (tabla pública) |
| GET | `/api/users/{id}` | — | Perfil por id |
| GET | `/api/users/{username}` | — | Perfil por username |
| GET | `/api/users/check-username` | — | Disponibilidad de username (`?username=&excludeId=`) |
| PUT | `/api/users/{id}` | JWT | Edita nickname/username/descripción (dueño o admin) |
| POST | `/api/users/{id}/avatar` | JWT | Sube el avatar (dueño o admin) |
| GET | `/api/users/{id}/avatar` | — | Archivo del avatar |
| POST | `/api/images` | JWT | Sube imagen (multipart) → storage + metadata + miniatura |
| GET | `/api/images` | — | Lista imágenes (`?userId=` para filtrar) |
| GET | `/api/images/{id}` | — | Metadata |
| GET | `/api/images/{id}/download` | — | Archivo original (`?download=true` para descargar) |
| GET | `/api/images/{id}/thumbnail` | — | Versión reducida para grids (genera bajo demanda si falta) |
| DELETE | `/api/images/{id}` | JWT | Borra (dueño o admin) |
| GET | `/api/saved` | JWT | Imágenes guardadas del usuario actual |
| POST | `/api/saved/{imageId}` | JWT | Guarda una imagen (idempotente) |
| DELETE | `/api/saved/{imageId}` | JWT | Quita una imagen de los guardados |
| GET | `/api/admin/users` | admin | Lista usuarios con email (solo rol `admin`) |
| WS | `/hubs/feed` | — | Hub de SignalR (eventos en vivo, ver sección Tiempo real) |

## Tiempo real (SignalR)

- Los clientes se conectan al hub `/hubs/feed` y reciben broadcasts del servidor.
- Eventos emitidos (contrato con el cliente):

| Evento | Payload | Cuándo |
|---|---|---|
| `ImageUploaded` | `ImageDto` | Se sube una imagen |
| `ImageDeleted` | `int` (id) | Se borra una imagen |
| `UserUpdated` | `UserDto` | Se edita perfil o avatar |

- Emitidos desde `Services/RealtimeService.cs` (puente con `IHubContext<FeedHub>`), invocado por `ImageService` y `AuthService`.
- Pendiente: autenticar el hub con JWT y agrupar por usuario para eventos privados/notificaciones.

## Miniaturas

- Al subir, el backend genera una miniatura (lado máximo configurable, JPEG/PNG/WebP/GIF según el original) y guarda su URL en `Images.ThumbnailUrl`.
- Los grids del cliente usan `/api/images/{id}/thumbnail`; el detalle de imagen usa el original.
- Las imágenes previas a esta feature no tienen miniatura: el endpoint la **genera bajo demanda** la primera vez y persiste el resultado.
- Ambos endpoints de archivo responden `Cache-Control: public, max-age=31536000, immutable` (las URLs son content-addressed, nunca cambian).

## Roles y permisos

- Columna `Role` en `Users`: `user` (por defecto) o `admin`.
- El rol viaja en el JWT (claim `role`) y se controla con `[Authorize(Roles = "admin")]`.
- **Promover a admin** (no hay endpoint a propósito):
  ```sql
  UPDATE dbo.Users SET Role = 'admin' WHERE Username = 'tu_usuario';
  ```
- Los admins pueden: ver `GET /api/admin/users` y borrar cualquier imagen.

## Scripts SQL

| Script | Para qué |
|---|---|
| `scripts/schema.sql` | DDL completo (borra y recrea). Instalación limpia |
| `scripts/migrations/0001_saved_images.sql` | Tabla `SavedImages` (idempotente, sobre base existente) |
| `scripts/migrations/0002_user_avatar.sql` | Columna `AvatarUrl` en `Users` (idempotente) |
| `scripts/migrations/0003_refresh_tokens.sql` | Tabla `RefreshTokens` (idempotente) |
| `scripts/consultas.sql` | Referencia de todas las consultas que ejecuta la API, por repositorio y endpoint |

Las migraciones son idempotentes: se pueden correr sobre una base que ya tiene datos sin perderlos.
Ejemplo:
```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts/migrations/0003_refresh_tokens.sql
```

## Estructura del proyecto

```
API/                proyecto web (Controllers, Models, Data, Services, Storage, Middleware, Hubs)
scripts/            schema.sql (DDL) + migrations/ (idempotentes) + consultas.sql (referencia)
CDN-backend.slnx    solución
ESTRUCTURA.md       plan y estado del proyecto
```
