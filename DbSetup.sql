﻿SET NOCOUNT ON

IF EXISTS(SELECT * FROM sys.tables where name = 'Tab1')
BEGIN
	DROP TABLE Tab1
END

CREATE TABLE Tab1
(
	Id INT NOT NULL IDENTITY(1,1),
	Txt NTEXT,
	StartDate DATETIME NOT NULL,
	DecVal DECIMAL(10, 3),
	FltVal FLOAT,
	PRIMARY KEY(Id)
)
GO

IF EXISTS(SELECT * FROM sys.objects WHERE type = 'P' AND name = 'GetFast')
BEGIN
	DROP PROCEDURE GetFast
END
GO

CREATE PROCEDURE GetFast
@Number AS INT
AS
BEGIN
SELECT * FROM Tab1 WHERE Id > @Number ORDER BY Id
END
GO



DECLARE @i INT = 1000000
DECLARE @d DECIMAL(10,3) = 2.0
DECLARE @f float = 1.0

WHILE @i > 0
BEGIN
	INSERT INTO Tab1(Txt, StartDate, DecVal, FltVal)	
		VALUES ('aaaaa', '2016-10-28', @d, @f)
	SET @d = @d + .1
	SET @f = @f + .1
	SET @i = @i -1
END
