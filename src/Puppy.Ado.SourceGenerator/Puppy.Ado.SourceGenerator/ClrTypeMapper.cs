using System.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Puppy.Ado.SourceGenerator
{
    public static class ClrTypeMapper
    {
        public static string ToClrType(SqlType sqlType, bool nullable, SqlConnection? connection = null)
        {
            var t = sqlType.Name.ToLowerInvariant();
            string typeName;
            bool isValueType;

            switch (t)
            {
                case "smallint":
                    typeName = "short"; isValueType = true; break;
                case "int":
                    typeName = "int"; isValueType = true; break;
                case "bigint":
                    typeName = "long"; isValueType = true; break;
                case "bit":
                    typeName = "bool"; isValueType = true; break;
                case "uniqueidentifier":
                    typeName = "System.Guid"; isValueType = true; break;
                case "decimal":
                case "numeric":
                    typeName = "decimal"; isValueType = true; break;
                case "float":
                    typeName = "double"; isValueType = true; break;
                case "real":
                    typeName = "float"; isValueType = true; break;
                case "date":
                case "datetime2":
                    typeName = "System.DateTime"; isValueType = true; break;
                case "datetimeoffset":
                    typeName = "System.DateTimeOffset"; isValueType = true; break;
                case "varbinary":
                    typeName = "byte[]"; isValueType = false; break;
                case "varchar":
                case "nvarchar":
                case "nchar":
                case "xml":
                    typeName = "string"; isValueType = false; break;
                default:
                    // fallback first; may be a table type — try lookup if connection string provided
                    typeName = "unknown"; isValueType = false; break;
            }

            switch (typeName)
            {
                // If fallback and a connection string is provided, try to resolve a table type (TVP) to a named tuple
                case "unknown" when connection != null:
                    try
                    {
                        var tuple = GetNamedTupleForTableType(sqlType.Name, connection);
                        if (!string.IsNullOrEmpty(tuple))
                        {
                            // ValueTuple types are structs; treat as value type so nullable handling is consistent
                            typeName = tuple;
                            isValueType = true;
                        }
                    }
                    catch
                    {
                        // swallow errors and fall back to "string"
                    }

                    break;
            }
            if (typeName == "unknown")
            {
                // final fallback
                typeName = "string";
                isValueType = false;
            }
            
            return nullable && isValueType ? typeName + "?" : typeName;
        }

        private static string GetNamedTupleForTableType(string fullTypeName, SqlConnection connection)
        {
            // Parse optional schema: "schema.TypeName" or "TypeName"
            var schema = "dbo";
            var typeName = fullTypeName.Trim().Trim('[', ']');
            var parts = typeName.Split(new[] { '.' }, 2);
            if (parts.Length == 2)
            {
                schema = parts[0].Trim('[', ']');
                typeName = parts[1].Trim('[', ']');
            }

            const string sql = @"
SELECT c.column_id, c.name AS column_name, typ.name AS data_type, c.max_length, c.precision, c.scale, c.is_nullable
FROM sys.table_types tt
JOIN sys.types t ON t.user_type_id = tt.user_type_id
JOIN sys.columns c ON c.object_id = tt.type_table_object_id
JOIN sys.types typ ON c.user_type_id = typ.user_type_id
WHERE t.name = @typeName AND SCHEMA_NAME(t.schema_id) = @schema
ORDER BY c.column_id;";

            var columns = new List<(string ColumnName, string SqlType, bool IsNullable)>();

            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add(new SqlParameter("@typeName", SqlDbType.NVarChar) { Value = typeName });
                cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar) { Value = schema });
                
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var colName = rdr.GetString(rdr.GetOrdinal("column_name"));
                    var sqlType = rdr.GetString(rdr.GetOrdinal("data_type"));
                    var isNullable = rdr.GetBoolean(rdr.GetOrdinal("is_nullable"));
                    columns.Add((colName, sqlType, isNullable));
                }
            }

            if (!columns.Any())
                return string.Empty;

            // Map SQL types to CLR and sanitize names
            var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var tupleParts = new List<string>();
            foreach (var col in columns)
            {
                var clr = MapSqlToClr(col.SqlType, col.IsNullable);
                var name = SanitizeIdentifier(col.ColumnName);
                if (usedNames.TryGetValue(name, out var count))
                {
                    count++;
                    usedNames[name] = count;
                    name = $"{name}_{count}";
                }
                else
                {
                    usedNames[name] = 1;
                }

                tupleParts.Add($"{clr} {name}");
            }

            return "(" + string.Join(", ", tupleParts) + ")";
        }

        private static string MapSqlToClr(string sqlTypeName, bool nullable)
        {
            var t = sqlTypeName.ToLowerInvariant();
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

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "_";

            // Replace invalid characters with underscore
            var sanitized = Regex.Replace(name, @"[^\p{L}\p{Nd}_]", "_");

            // If starts with digit, prefix underscore
            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            // Avoid empty result
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "_";

            // Escape C# keyword by prefixing @ if needed
            if (IsCSharpKeyword(sanitized))
                sanitized = "@" + sanitized;

            return sanitized;
        }

        private static bool IsCSharpKeyword(string identifier)
        {
            // minimal set of common keywords — extend if necessary
            var keywords = new HashSet<string>(StringComparer.Ordinal)
            {
                "class","namespace","public","private","internal","protected","void","int","long","string","bool","byte","new","return","var"
            };
            return keywords.Contains(identifier);
        }
    }
}
