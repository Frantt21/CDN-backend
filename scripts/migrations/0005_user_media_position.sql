-- Migración 0005: posición/zoom de avatar y banner del perfil.
-- Guarda el JSON {"x":50,"y":50,"zoom":1} que produce el editor del cliente
-- (drag + zoom), para que todos los contenedores rendericen igual.
-- Corré este script contra la base CDNBackend:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts\migrations\0005_user_media_position.sql
IF COL_LENGTH('dbo.Users', 'AvatarPosition') IS NULL
    ALTER TABLE dbo.Users ADD AvatarPosition NVARCHAR(64) NULL;
GO

IF COL_LENGTH('dbo.Users', 'BannerPosition') IS NULL
    ALTER TABLE dbo.Users ADD BannerPosition NVARCHAR(64) NULL;
GO
