-- ============================================================
-- scripts/consultas.sql
-- Referencia de TODAS las consultas SQL que ejecuta la API (Dapper),
-- agrupadas por repositorio y anotadas con el endpoint que las usa.
-- Sirven para entender y probar qué hace cada llamada.
--
-- Conexión dev:  Server=(localdb)\MSSQLLocalDB ; Database=CDNBackend
-- En VS podés ejecutarlas desde SQL Server Object Explorer,
-- reemplazando los @parámetros por valores reales.
-- ============================================================

USE CDNBackend;
GO

-- ============================================================
-- 1) USUARIOS (tabla pública) — API/Data/UsersRepository.cs
--    Columnas: Id, Nickname, Username, Role, Description,
--              AvatarUrl, CreatedAt
-- ============================================================

-- GET /api/users — listar todos los usuarios (público)
SELECT Id, Nickname, Username, Role, Description, AvatarUrl, CreatedAt
FROM Users
ORDER BY CreatedAt DESC;

-- GET /api/users/{id} — perfil por id (público)
SELECT Id, Nickname, Username, Role, Description, AvatarUrl, CreatedAt
FROM Users
WHERE Id = @Id;

-- GET /api/users/{username} — perfil por username (público)
SELECT Id, Nickname, Username, Role, Description, AvatarUrl, CreatedAt
FROM Users
WHERE Username = @Username;

-- GET /api/users/check-username — validación en vivo de disponibilidad.
-- Se usa en registro y edición de perfil. @ExcludeId permite ignorar
-- al propio usuario al editar (su username actual cuenta como libre).
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Users
    WHERE Username = @Username
      AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
) THEN 1 ELSE 0 END;

-- POST /api/auth/register — insertar usuario (dentro de una transacción).
-- Role y CreatedAt no se insertan: toman sus DEFAULT ('user', hora actual).
INSERT INTO Users (Nickname, Username, Description, CreatedAt)
VALUES (@Nickname, @Username, @Description, @CreatedAt);
SELECT CAST(SCOPE_IDENTITY() AS INT);   -- devuelve el Id generado

-- PUT /api/users/{id} — editar perfil (dueño o admin)
UPDATE Users
SET Nickname = @Nickname,
    Username = @Username,
    Description = @Description
WHERE Id = @Id;

-- POST /api/users/{id}/avatar — asignar avatar (dueño o admin).
-- El archivo se sube al storage antes; acá solo se guarda la URL.
UPDATE Users
SET AvatarUrl = @Url
WHERE Id = @Id;

-- POST /api/users/{id}/banner — asignar banner del perfil (dueño o admin).
-- Igual que el avatar: el archivo se sube al storage antes.
UPDATE Users
SET BannerUrl = @Url
WHERE Id = @Id;

-- GET /api/admin/users — usuarios con email (solo rol admin).
-- Une la tabla pública con la privada (UserCredentials).
SELECT u.Id, u.Nickname, u.Username, u.Role, u.CreatedAt, c.Email
FROM Users u
JOIN UserCredentials c ON c.UserId = u.Id
ORDER BY u.CreatedAt DESC;

-- Promover un usuario a admin (no hay endpoint a propósito):
--   UPDATE Users SET Role = 'admin' WHERE Username = 'tu_usuario';

-- ============================================================
-- 2) CREDENCIALES (tabla privada) — API/Data/AuthRepository.cs
--    Columnas: Id, UserId, Email, PasswordHash (hash Argon2id)
-- ============================================================

-- POST /api/auth/login — buscar credencial por email
SELECT Id, UserId, Email, PasswordHash
FROM UserCredentials
WHERE Email = @Email;

-- POST /api/auth/register — ¿el email ya está registrado?
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM UserCredentials WHERE Email = @Email
) THEN 1 ELSE 0 END;

-- POST /api/auth/register — insertar credencial (misma transacción que el usuario)
INSERT INTO UserCredentials (UserId, Email, PasswordHash)
VALUES (@UserId, @Email, @PasswordHash);
SELECT CAST(SCOPE_IDENTITY() AS INT);

-- ============================================================
-- 3) REFRESH TOKENS (rotación de sesión JWT) — AuthRepository.cs
--    Se guarda el HASH del token, nunca el valor. 1:N con Users.
-- ============================================================

-- POST /api/auth/register|login|refresh — emitir un refresh token nuevo
INSERT INTO RefreshTokens (UserId, TokenHash, ExpiresAt)
VALUES (@UserId, @TokenHash, @ExpiresAt);
SELECT CAST(SCOPE_IDENTITY() AS INT);

-- POST /api/auth/refresh — buscar el refresh token por su hash.
-- La API valida que no esté revocado (RevokedAt IS NULL) y que no haya expirado.
SELECT Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt
FROM RefreshTokens
WHERE TokenHash = @TokenHash;

-- POST /api/auth/refresh — rotación: revoca el refresh token usado
-- (el que se validó arriba) antes de emitir uno nuevo.
UPDATE RefreshTokens
SET RevokedAt = SYSUTCDATETIME()
WHERE TokenHash = @TokenHash;

-- ============================================================
-- 4) IMÁGENES (metadatos) — API/Data/ImagesRepository.cs
--    Columnas: Id, UserId, Name, Description, Url, ThumbnailUrl,
--              ContentType, SizeBytes, CreatedAt
--    El binario vive en el storage/CDN (Url); ThumbnailUrl es la
--    versión reducida (max 800px) que usan los grids.
-- ============================================================

-- POST /api/images — insertar metadata de la imagen subida
-- (ThumbnailUrl se genera al subir; puede ser NULL si falló o en
-- imágenes previas a esta feature)
INSERT INTO Images (UserId, Name, Description, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt)
VALUES (@UserId, @Name, @Description, @Url, @ThumbnailUrl, @ContentType, @SizeBytes, @CreatedAt);
SELECT CAST(SCOPE_IDENTITY() AS INT);

-- GET /api/images — listar todas las imágenes
SELECT Id, UserId, Name, Description, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt
FROM Images
ORDER BY CreatedAt DESC;

-- GET /api/images?userId= — listar las imágenes de un usuario
SELECT Id, UserId, Name, Description, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt
FROM Images
WHERE UserId = @UserId
ORDER BY CreatedAt DESC;

-- GET /api/images/{id} — metadata de una imagen
SELECT Id, UserId, Name, Description, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt
FROM Images
WHERE Id = @Id;

-- GET /api/images/{id}/thumbnail — guardar la miniatura generada
-- bajo demanda (solo cuando la imagen no tenía ThumbnailUrl)
UPDATE Images
SET ThumbnailUrl = @Url
WHERE Id = @Id;

-- DELETE /api/images/{id} — borrar (dueño o admin; el archivo se
-- borra del storage antes de esta consulta). También borra los
-- guardados de esa imagen (el FK a Images es NO ACTION).
DELETE FROM SavedImages WHERE ImageId = @Id;
DELETE FROM Images WHERE Id = @Id;

-- ============================================================
-- 5) GUARDADOS (favoritos) — API/Data/SavedImagesRepository.cs
-- ============================================================

-- POST /api/saved/{imageId} — guardar (idempotente: si ya estaba
-- guardada la fila no se inserta otra vez; devuelve filas afectadas)
INSERT INTO SavedImages (UserId, ImageId)
SELECT @UserId, @ImageId
WHERE NOT EXISTS (
    SELECT 1 FROM SavedImages
    WHERE UserId = @UserId AND ImageId = @ImageId
);

-- DELETE /api/saved/{imageId} — quitar de guardados
DELETE FROM SavedImages WHERE UserId = @UserId AND ImageId = @ImageId;

-- GET /api/saved — imágenes guardadas del usuario actual,
-- unidas a los metadatos de Images, ordenadas por fecha de guardado
SELECT i.Id, i.UserId, i.Name, i.Description, i.Url, i.ThumbnailUrl, i.ContentType, i.SizeBytes, i.CreatedAt
FROM SavedImages s
JOIN Images i ON i.Id = s.ImageId
WHERE s.UserId = @UserId
ORDER BY s.CreatedAt DESC;
