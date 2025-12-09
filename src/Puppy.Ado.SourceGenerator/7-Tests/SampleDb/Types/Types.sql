CREATE TYPE dbo.PersonName from varchar(100) not null;
go

CREATE TYPE dbo.Age from smallint not null;
go

CREATE TYPE ClassesType AS TABLE (
                                           LocationName VARCHAR(50),
                                           Level INT
                                       );
GO