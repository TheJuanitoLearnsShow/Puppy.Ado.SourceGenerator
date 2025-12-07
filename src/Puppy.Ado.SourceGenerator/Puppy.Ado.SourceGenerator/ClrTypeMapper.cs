namespace Puppy.Ado.SourceGenerator
{
    public static class ClrTypeMapper
    {
        public static string ToClrType(SqlType sqlType, bool nullable)
        {
            var t = sqlType.Name.ToLowerInvariant();
            var (typeName, isValueType) = t switch
            {
                "int" => ("int", true),
                "bigint" => ("long", true),
                "bit" => ("bool", true),
                "uniqueidentifier" => ("System.Guid", true),
                "decimal" or "numeric" => ("decimal", true),
                "float" => ("double", true),
                "real" => ("float", true),
                "date" or "datetime2" => ("System.DateTime", true),
                "datetimeoffset" => ("System.DateTimeOffset", true),
                "varbinary" => ("byte[]", false),
                "varchar" or "nvarchar" or "nchar" or "xml" => ("string", false),
                _ => ("string", false)
            };
            return nullable && isValueType ? typeName + "?" : typeName;
        }
    }
}