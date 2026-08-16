-- Migración 0004: banner del perfil de usuario.
-- Agrega la columna BannerUrl a la tabla Users (idempotente).
-- El banner es la imagen de fondo de la sección del perfil (como el avatar,
-- se sube desde el cliente y se guarda la URL en la columna).
-- Corré este script contra la base CDNBackend:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts\migrations\0004_user_banner.sql
IF COL_LENGTH('dbo.Users', 'BannerUrl') IS NULL
    ALTER TABLE dbo.Users ADD BannerUrl NVARCHAR(500) NULL;
GO
