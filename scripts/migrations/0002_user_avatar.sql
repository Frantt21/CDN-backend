-- Migración 0002: avatar de usuario.
-- Agrega la columna AvatarUrl a la tabla Users (idempotente).
-- Corré este script contra la base CDNBackend:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts\migrations\0002_user_avatar.sql
IF COL_LENGTH('dbo.Users', 'AvatarUrl') IS NULL
    ALTER TABLE dbo.Users ADD AvatarUrl NVARCHAR(500) NULL;
GO
