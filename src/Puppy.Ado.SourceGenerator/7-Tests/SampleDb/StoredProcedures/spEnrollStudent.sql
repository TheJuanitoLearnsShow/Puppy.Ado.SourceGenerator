CREATE PROCEDURE [dbo].[spEnrollStudent]
  @FirstName dbo.PersonName,
  @LastName dbo.PersonName,
  @Age dbo.Age,
  @ClassesToEnroll ClassesType READONLY
AS
  SELECT @FirstName FirstName, @LastName LastName, @Age Age
RETURN 0