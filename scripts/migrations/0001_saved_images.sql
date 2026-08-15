-- ============================================================
-- scripts/migrations/0001_saved_images.sql
-- Tabla SavedImages (Guardados). Idempotente: se puede correr
-- sobre una base existente sin perder datos (a diferencia de
-- re-ejecutar scripts/schema.sql, que borra todo).
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d CDNBackend -i scripts/migrations/0001_saved_images.sql
-- ============================================================

IF OBJECT_ID(N'dbo.SavedImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SavedImages
    (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SavedImages PRIMARY KEY,
        UserId    INT       NOT NULL CONSTRAINT FK_SavedImages_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        ImageId   INT       NOT NULL CONSTRAINT FK_SavedImages_Images REFERENCES dbo.Images (Id),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SavedImages_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_SavedImages_User_Image UNIQUE (UserId, ImageId)
    );

    CREATE INDEX IX_SavedImages_UserId ON dbo.SavedImages (UserId);
END;
GO

-- Nota: el FK a Images es NO ACTION (sin CASCADE) porque SQL Server
-- rechaza dos rutas de cascade sobre la misma tabla (User->Images y
-- User->SavedImages). La API borra los guardados de una imagen antes
-- de eliminarla (ver ImagesRepository.DeleteAsync).
