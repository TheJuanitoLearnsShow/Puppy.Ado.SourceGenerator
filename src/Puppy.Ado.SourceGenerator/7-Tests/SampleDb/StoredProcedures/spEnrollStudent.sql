CREATE PROCEDURE [dbo].[spEnrollStudent]
  @FirstName dbo.PersonName,
  @LastName dbo.PersonName,
  @Age dbo.Age,
  @GPA dbo.GPA,
  @ClassesToEnroll ClassesType READONLY
AS
  SELECT @FirstName FirstName, @LastName LastName, @Age Age, @GPA GPA;
RETURN 0