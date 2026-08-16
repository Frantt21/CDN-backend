# Estructura del proyecto — CDN-backend

API REST en **C# / .NET 10 + SQL Server** que almacena usuarios, imágenes (metadatos + CDN) y sus URLs.
El frontend es una app **React** que vive en este mismo repo pero **ignorada por git** (`CDN-client/`).

---

## 1. Stack

| Capa | Tecnología | Notas |
|---|---|---|
| API | ASP.NET Core Web API (net10.0) | VS 2022+ |
| Base de datos | SQL Server | LocalDB o SQL Server Express en dev |
| Acceso a datos | **Dapper** | micro-ORM, SQL a mano |
| Contraseñas | **Argon2id** (`Konscious.Security.Cryptography`) | nunca SHA-256 plano |
| Auth | **JWT Bearer** | el frontend envía `Authorization: Bearer <token>` |
| Imágenes | CDN (Azure Blob Storage en prod, disco local en dev) | abstraído por interfaz |
| Frontend | React + Vite | en `client/`, gitignored |

---

## 2. Estructura de carpetas

```
CDN-backend/
├── CDN-backend.slnx
├── API/                          # proyecto web CDNBackend.API (net10.0)
│   ├── Controllers/
│   │   ├── AuthController.cs     # POST /api/auth/register, /api/auth/login
│   │   ├── UsersController.cs    # GET /api/users, /api/users/{id|username}
│   │   ├── ImagesController.cs   # POST/GET/DELETE /api/images
│   │   └── SavedImagesController.cs # GET/POST/DELETE /api/saved (guardados)
│   ├── Models/
│   │   ├── Entities/             # User, UserCredential, Image, SavedImage
│   │   └── Dtos/                 # RegisterRequest, LoginRequest, UserDto, ImageDto...
│   ├── Data/
│   │   ├── Database.cs           # fábrica de IDbConnection (SQL Server)
│   │   ├── UsersRepository.cs    # Dapper: consultas de la tabla pública
│   │   ├── AuthRepository.cs     # Dapper: email/hash (tabla privada)
│   │   ├── ImagesRepository.cs   # Dapper: metadata de imágenes
│   │   └── SavedImagesRepository.cs # Dapper: guardados por usuario
│   ├── Services/
│   │   ├── PasswordHasher.cs     # Argon2id (hash + verify)
│   │   ├── JwtService.cs         # emisión/validación de tokens
│   │   ├── AuthService.cs        # registro, login, normalización de username
│   │   ├── ImageService.cs       # validación + subida al CDN + metadata
│   │   └── SavedImageService.cs  # lógica de guardar/desguardar/listar
│   ├── Storage/
│   │   ├── IImageStorage.cs      # Upload / GetUrl / Delete
│   │   ├── AzureBlobStorage.cs   # CDN real (producción)
│   │   └── LocalDiskStorage.cs   # desarrollo (wwwroot/uploads)
│   ├── Middleware/               # (opcional) manejo de errores global
│   ├── Program.cs                # DI, JWT, CORS, Swagger
│   └── appsettings.json
├── CDN-client/                   # app React (NO se commitea → .gitignore)
├── scripts/
│   └── schema.sql                # CREATE DATABASE + tablas + índices
├── .gitignore
└── ESTRUCTURA.md
```

---

## 3. Base de datos (SQL Server)

### Tabla pública `Users`
Expuesta tal cual por la API (sin email, sin hash).

| Columna | Tipo | Notas |
|---|---|---|
| Id | INT IDENTITY (PK) | alternativa: UNIQUEIDENTIFIER |
| Nickname | NVARCHAR(50) NOT NULL | visible, puede llevar espacios/emojis |
| Username | NVARCHAR(50) NOT NULL | **normalizado**: solo `[a-z0-9_]`, minúsculas, UNIQUE |
| Description | NVARCHAR(500) NULL | |
| CreatedAt | DATETIME2 NOT NULL | UTC |

### Tabla privada `UserCredentials`
Solo la lee el backend para login/registro. Relación 1:1 con `Users`.

| Columna | Tipo | Notas |
|---|---|---|
| Id | INT IDENTITY (PK) | |
| UserId | INT NOT NULL (FK → Users.Id) | UNIQUE (1:1) |
| Email | NVARCHAR(320) NOT NULL | UNIQUE |
| PasswordHash | NVARCHAR(255) NOT NULL | Argon2id (`$argon2id$...`) |

### Tabla `Images`
Metadatos; el binario vive en el CDN.

| Columna | Tipo | Notas |
|---|---|---|
| Id | INT IDENTITY (PK) | |
| UserId | INT NOT NULL (FK → Users.Id) | quién la subió |
| Name | NVARCHAR(255) NOT NULL | |
| Description | NVARCHAR(500) NULL | |
| Url | NVARCHAR(500) NOT NULL | URL del CDN (vista/descarga) |
| ContentType | NVARCHAR(100) NOT NULL | image/png, image/jpeg… |
| SizeBytes | BIGINT NULL | opcional |
| CreatedAt | DATETIME2 NOT NULL | fecha de subida |

> La "url" del enunciado queda cubierta por `Images.Url`. Si más adelante querés
> enlaces independientes (una tabla de links), se agrega una tabla `Links` análoga.

### Tabla `SavedImages` (guardados / bookmarks)
Cada fila marca una imagen guardada por un usuario. La UNIQUE `(UserId, ImageId)`
hace que guardar dos veces sea idempotente (misma fila).

| Columna | Tipo | Notas |
|---|---|---|
| Id | INT IDENTITY (PK) | |
| UserId | INT NOT NULL (FK → Users.Id) | quién guarda |
| ImageId | INT NOT NULL (FK → Images.Id) | qué imagen |
| CreatedAt | DATETIME2 NOT NULL | fecha de guardado |

> Borrar un usuario (ON DELETE CASCADE) elimina sus filas de guardados. Al borrar
> una imagen, la API elimina antes sus guardados (`ImagesRepository.DeleteAsync`),
> porque el FK a `Images` es NO ACTION (SQL Server rechaza dos rutas de cascade).

### Tabla `RefreshTokens` (renovación de sesión)
Cada fila es un refresh token **hasheado (SHA-256)**; el token en claro nunca se guarda.
`ExpiresAt` (7 días por defecto) y `RevokedAt` permiten rotación y revocación.

| Columna | Tipo | Notas |
|---|---|---|
| Id | INT IDENTITY (PK) | |
| UserId | INT NOT NULL (FK → Users.Id) | ON DELETE CASCADE |
| TokenHash | NVARCHAR(128) NOT NULL | SHA-256 en hexa (indexado) |
| ExpiresAt | DATETIME2 NOT NULL | momento de expiración (UTC) |
| CreatedAt | DATETIME2 NOT NULL | default `SYSUTCDATETIME()` |
| RevokedAt | DATETIME2 NULL | se setea al rotar/revocar |

---

## 4. Endpoints

| Método | Ruta | Público | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | Sí | crea usuario + credenciales → devuelve JWT + refresh token |
| POST | `/api/auth/login` | Sí | valida email + Argon2 → devuelve JWT + refresh token |
| POST | `/api/auth/refresh` | Sí | rota el refresh token → devuelve JWT nuevo + refresh token nuevo |
| GET | `/api/users` | Sí | lista usuarios (solo tabla pública) |
| GET | `/api/users/{id}` | Sí | perfil por id |
| GET | `/api/users/{username}` | Sí | perfil por username |
| PUT | `/api/users/{id}` | JWT | edita nickname/username/descripción (dueño o admin) |
| POST | `/api/users/{id}/avatar` | JWT | sube avatar multipart (dueño o admin) → `AvatarUrl` |
| GET | `/api/users/{id}/avatar` | Sí | stream del avatar (404 si no tiene) |
| POST | `/api/images` | JWT | sube imagen (multipart) → CDN + metadata |
| GET | `/api/images` | Sí | lista imágenes (filtro por usuario) |
| GET | `/api/images/{id}` | Sí | metadata |
| GET | `/api/images/{id}/download` | Sí | descarga/stream desde el CDN |
| DELETE | `/api/images/{id}` | JWT | borra del CDN + metadata (solo dueño) |
| GET | `/api/saved` | JWT | imágenes guardadas del usuario actual |
| POST | `/api/saved/{imageId}` | JWT | guarda una imagen (idempotente) |
| DELETE | `/api/saved/{imageId}` | JWT | quita una imagen de los guardados |

---

## 5. Seguridad

- **Contraseñas:** Argon2id vía `Konscious.Security.Cryptography`. El hash se guarda
  con salt incluido; nunca se almacena ni devuelve la contraseña.
- **Normalización de username:** el registro valida `^[a-zA-Z0-9_]+$` y guarda en
  minúsculas (evita duplicados tipo `Maria`/`maria`). Caracteres inválidos → 400.
- **JWT:** expiración configurable, clave en `appsettings`/variables de entorno.
- **CORS:** restringido al origen del frontend (p. ej. `http://localhost:5173` en dev).
- **Emails y hashes** solo existen en la tabla privada; ningún endpoint público los expone (solo `GET /api/admin/users`, restringido a rol `admin`).
- **Roles:** columna `Role` en `Users` (`user` por defecto, `admin` opcional). El rol viaja en el JWT (claim `role`) y se controla con `[Authorize(Roles = "admin")]`. Promover admin: `UPDATE dbo.Users SET Role='admin' WHERE Username='...';`

---

## 6. CDN de imágenes

- `IImageStorage` abstrae el proveedor: **Azure Blob Storage (CDN)** en producción,
  **disco local** (`wwwroot/uploads`) en desarrollo — el resto del código no cambia.
- `Images.Url` apunta al CDN; `/download` puede redirigir o firmar la URL.
- Antes de subir: validar `Content-Type` (imágenes) y tamaño máximo (p. ej. 10 MB).

---

## 7. Frontend React (gitignored)

- `CDN-client/` se creó con Vite + React + React Router dentro del repo pero **no se trackea**.
- En `.gitignore`: `client/` y `CDN-client/`.
- Se autentica con JWT, guarda el token en localStorage y consume la API vía el proxy `/api` de Vite.

> ✅ El `.gitignore` ya existe y cubre `.vs/`, `bin/`, `obj/` y `client/`.

---

## 8. Estado actual y próximos pasos

✅ **Hecho:**

1. Proyecto renombrado a `API/` (CDNBackend.API) + solución `CDN-backend.slnx`.
2. `.gitignore` creado (incluye `client/` para el futuro frontend).
3. Paquetes: `Dapper`, `Microsoft.Data.SqlClient`, `Konscious.Security.Cryptography.Argon2`, `Microsoft.AspNetCore.Authentication.JwtBearer`.
4. `scripts/schema.sql` + base `CDNBackend` creada en LocalDB (3 tablas).
5. Scaffold completo: repos (Dapper) → services (Argon2, JWT, auth, imágenes) → controllers → middleware de errores.
6. Probado de punta a punta: registro, login, normalización, subida/descarga/borrado de imágenes, autorización.
7. **Roles** (`user`/`admin`) en la DB + claim en el JWT + endpoints admin (`GET /api/admin/users`, borrar cualquier imagen).
8. **Edición de perfil**: `PUT /api/users/{id}` (dueño o admin) + botón "Editar perfil" en el cliente.
9. Cliente React en `CDN-client/` (Vite + React Router, gitignored) con login/registro, galería, subida y perfil.
10. **Guardados**: tabla `SavedImages` + `GET/POST/DELETE /api/saved` (repo `SavedImagesRepository`, service `SavedImageService`, controller `SavedImagesController`). Migración: `scripts/migrations/0001_saved_images.sql`.

11. **Avatar de usuario**: columna `Users.AvatarUrl` + `POST/GET /api/users/{id}/avatar` (el archivo se sube al mismo storage que las imágenes y se reemplaza al subir otro). Migración: `scripts/migrations/0002_user_avatar.sql`.

12. **Refresh tokens**: tabla `RefreshTokens` + `POST /api/auth/refresh`. El frontend renueva la sesión automáticamente cuando el access token (120 min) expira; el refresh token (7 días) se rota en cada uso. Migración: `scripts/migrations/0003_refresh_tokens.sql`.

⏳ **Pendiente:**

1. Reemplazar la `Jwt:Key` de desarrollo por una real (user-secrets o variable de entorno).
2. Implementar `AzureBlobStorage` (paquete `Azure.Storage.Blobs`) para el CDN en producción.
3. UI de administración en el cliente (listar usuarios con email, panel de admin).
