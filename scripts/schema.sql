-- ============================================================
-- scripts/schema.sql — Base CDNBackend (SQL Server)
-- Dev (LocalDB):  Server=(localdb)\MSSQLLocalDB
-- Ejecutar con VS (SQL Server Object Explorer) o:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -i scripts/schema.sql
-- ============================================================

IF DB_ID(N'CDNBackend') IS NULL
    CREATE DATABASE CDNBackend;
GO

USE CDNBackend;
GO

IF OBJECT_ID(N'dbo.SavedImages', N'U') IS NOT NULL DROP TABLE dbo.SavedImages;
IF OBJECT_ID(N'dbo.Images', N'U') IS NOT NULL DROP TABLE dbo.Images;
IF OBJECT_ID(N'dbo.UserCredentials', N'U') IS NOT NULL DROP TABLE dbo.UserCredentials;
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- ------------------------------------------------------------
-- Tabla pública: perfil expuesto por la API (sin email/hash)
-- ------------------------------------------------------------
CREATE TABLE dbo.Users
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Nickname    NVARCHAR(50)  NOT NULL,
    Username    NVARCHAR(50)  NOT NULL, -- normalizado: [a-z0-9_]
    Role        NVARCHAR(20)  NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'user', -- 'user' | 'admin'
    Description NVARCHAR(500) NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users (Username);
GO

-- Para promover a admin a un usuario ya registrado:
--   UPDATE dbo.Users SET Role = 'admin' WHERE Username = 'tu_usuario';
-- Los roles se incluyen en el JWT (claim 'role') y se verifican con
-- [Authorize(Roles = "admin")] en los endpoints del API.

-- ------------------------------------------------------------
-- Tabla privada: email + hash Argon2. Solo la usa el backend
-- para los formularios de login/registro. 1:1 con Users.
-- ------------------------------------------------------------
CREATE TABLE dbo.UserCredentials
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserCredentials PRIMARY KEY,
    UserId       INT           NOT NULL CONSTRAINT FK_UserCredentials_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    Email        NVARCHAR(320) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    CONSTRAINT UX_UserCredentials_UserId UNIQUE (UserId)
);
GO

CREATE UNIQUE INDEX UX_UserCredentials_Email ON dbo.UserCredentials (Email);
GO

-- ------------------------------------------------------------
-- Metadatos de imágenes. El binario vive en el CDN (Images.Url)
-- ------------------------------------------------------------
CREATE TABLE dbo.Images
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Images PRIMARY KEY,
    UserId      INT           NOT NULL CONSTRAINT FK_Images_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    Name        NVARCHAR(255) NOT NULL,
    Description NVARCHAR(500) NULL,
    Url         NVARCHAR(500) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    SizeBytes   BIGINT        NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Images_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX IX_Images_UserId ON dbo.Images (UserId);
GO

-- ------------------------------------------------------------
-- Guardados: el usuario marca imágenes como favoritas (bookmarks).
-- La UNIQUE (UserId, ImageId) hace que guardar dos veces sea idempotente.
-- El FK a Images es NO ACTION (no CASCADE): SQL Server rechaza dos rutas
-- de cascade (User->Images y User->SavedImages). La API borra los
-- guardados de una imagen antes de eliminarla (ImagesRepository.DeleteAsync).
-- ------------------------------------------------------------
CREATE TABLE dbo.SavedImages
(
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SavedImages PRIMARY KEY,
    UserId    INT       NOT NULL CONSTRAINT FK_SavedImages_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    ImageId   INT       NOT NULL CONSTRAINT FK_SavedImages_Images REFERENCES dbo.Images (Id),
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SavedImages_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UX_SavedImages_User_Image UNIQUE (UserId, ImageId)
);
GO

CREATE INDEX IX_SavedImages_UserId ON dbo.SavedImages (UserId);
GO
