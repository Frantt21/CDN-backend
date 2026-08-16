-- Migración 0006: categoría de imagen.
-- Se muestra solo en el detalle de la imagen, no en las cards.
-- Corré este script contra la base CDNBackend:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts\migrations\0006_image_category.sql
IF COL_LENGTH('dbo.Images', 'Category') IS NULL
    ALTER TABLE dbo.Images ADD Category NVARCHAR(64) NULL;
GO
