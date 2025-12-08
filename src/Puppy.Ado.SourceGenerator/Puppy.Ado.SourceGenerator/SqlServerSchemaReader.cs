using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
// C#
namespace System.Runtime.CompilerServices
{
    // Minimal shim for C# 9 init-only setters when targeting older TFMs
}

namespace Puppy.Ado.SourceGenerator
{
    public sealed class SqlServerSchemaReader
    {
        private readonly string _connectionString;

        public SqlServerSchemaReader(string connectionString) => _connectionString = connectionString;

        public async Task<DatabaseModel> ReadAsync(CancellationToken ct = default)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            var tableTypes = await ReadTableTypesAsync(conn, ct);
            var procs = await ReadProceduresAsync(conn, ct);
            var funcs = await ReadFunctionsAsync(conn, ct);
            var views = await ReadViewsAsync(conn, ct);

            return new DatabaseModel
            {
                TableTypes = tableTypes,
                StoredProcedures = procs,
                Functions = funcs,
                Views = views
            };
        }

        private static async Task<IReadOnlyList<TableTypeModel>> ReadTableTypesAsync(SqlConnection conn, CancellationToken ct)
        {
            const string sql = @"
SELECT s.name AS SchemaName, tt.name AS TypeName, c.name AS ColumnName, 
       t.name AS SqlType, c.max_length, c.precision, c.scale, c.is_nullable,
       c.column_id AS Ordinal
FROM sys.table_types tt
JOIN sys.schemas s ON s.schema_id = tt.schema_id
JOIN sys.columns c ON c.object_id = tt.type_table_object_id
JOIN sys.types t ON t.user_type_id = c.user_type_id
ORDER BY s.name, tt.name, c.column_id";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var list = new List<TableTypeModel>();
            TableTypeModel? current = null;
            string? prevSchema = null, prevName = null;
            var cols = new List<ResultColumnModel>();

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);

                if (prevSchema != schema || prevName != name)
                {
                    if (current is not null)
                    {
                        current = current with { Columns = cols.ToArray() };
                        list.Add(current);
                        cols.Clear();
                    }
                    current = new TableTypeModel { Schema = schema, Name = name };
                    prevSchema = schema;
                    prevName = name;
                }

                var col = new ResultColumnModel
                {
                    Name = reader.GetString(2),
                    SqlType = MapSqlType(reader.GetString(3), reader.GetInt16(4), reader.GetByte(5), reader.GetByte(6)),
                    IsNullable = reader.GetBoolean(7),
                    Ordinal = reader.GetInt32(8)
                };
                cols.Add(col);
            }

            if (current is not null)
            {
                current = current with { Columns = cols.ToArray() };
                list.Add(current);
            }

            return list;
        }

        private static async Task<IReadOnlyList<StoredProcedureModel>> ReadProceduresAsync(SqlConnection conn, CancellationToken ct)
        {
            const string procSql = @"
SELECT SPECIFIC_SCHEMA, SPECIFIC_NAME
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_TYPE = 'PROCEDURE'
ORDER BY SPECIFIC_SCHEMA, SPECIFIC_NAME";

            var procs = new List<StoredProcedureModel>();
            using (var cmd = new SqlCommand(procSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    procs.Add(new StoredProcedureModel
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1)
                    });
                }
            }

            foreach (var p in procs)
            {
                p.Parameters = await ReadParametersAsync(conn, p.Schema, p.Name, ct);
                p.ResultSets = await DescribeResultSetsAsync(conn, p.Schema, p.Name, ct);
            }

            return procs;
        }

        private static async Task<IReadOnlyList<ParameterModel>> ReadParametersAsync(SqlConnection conn, string schema, string name, CancellationToken ct)
        {
            const string paramSql = @"
        SELECT PARAMETER_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
               PARAMETER_MODE, IS_RESULT, USER_DEFINED_TYPE_SCHEMA, USER_DEFINED_TYPE_NAME
        FROM INFORMATION_SCHEMA.PARAMETERS
        WHERE SPECIFIC_SCHEMA = @schema AND SPECIFIC_NAME = @name
        ORDER BY ORDINAL_POSITION";

            using var cmd = new SqlCommand(paramSql, conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@name", name);

            var list = new List<ParameterModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var dataType = reader.GetString(1);
                var len = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var prec = reader.IsDBNull(3) ? (int?)null : reader.GetByte(3);
                var scale = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                var mode = reader.GetString(5);
                var isOutput = string.Equals(mode, "OUT", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(mode, "INOUT", StringComparison.OrdinalIgnoreCase);

                var udtSchema = reader.IsDBNull(7) ? null : reader.GetString(7);
                var udtName = reader.IsDBNull(8) ? null : reader.GetString(8);
                var isStructured = string.Equals(dataType, "table type", StringComparison.OrdinalIgnoreCase) ||
                                   (udtSchema is not null && udtName is not null);

                list.Add(new ParameterModel
                {
                    Name = reader.GetString(0),
                    SqlType = isStructured && udtSchema is not null && udtName is not null
                        ? SqlType.Structured($"[{udtSchema}].[{udtName}]")
                        : MapSqlType(dataType, len, prec, scale),
                    IsOutput = isOutput,
                    IsNullable = false,
                    IsTableValued = isStructured,
                    TableTypeFullName = isStructured && udtSchema is not null && udtName is not null ? $"[{udtSchema}].[{udtName}]" : null
                });
            }

            return list;
        }


        private static async Task<IReadOnlyList<ResultSetModel>> DescribeResultSetsAsync(SqlConnection conn, string schema, string name, CancellationToken ct)
        {
            // Use sys.sp_describe_first_result_set for first set; for multiple, you may add hints or instrumentation.
            using var cmd = new SqlCommand("sys.sp_describe_first_result_set", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@tsql", $"EXEC [{schema}].[{name}]");

            var sets = new List<ResultSetModel>();
            var cols = new List<ResultColumnModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            int ordinal = 0;
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var colName = reader.IsDBNull(2) ? $"Column{ordinal}" : reader.GetString(2);
                var sysTypeName = reader.GetString(5); // system_type_name
                var isNullable = reader.GetBoolean(3);

                cols.Add(new ResultColumnModel
                {
                    Name = colName,
                    SqlType = ParseSysType(sysTypeName),
                    IsNullable = isNullable,
                    Ordinal = ordinal++
                });
            }

            if (cols.Count > 0)
                sets.Add(new ResultSetModel { Index = 0, Columns = cols.ToArray() });

            return sets;
        }

        private static async Task<IReadOnlyList<FunctionModel>> ReadFunctionsAsync(SqlConnection conn, CancellationToken ct)
        {
            const string fnSql = @"
SELECT SPECIFIC_SCHEMA, SPECIFIC_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_TYPE = 'FUNCTION'
ORDER BY SPECIFIC_SCHEMA, SPECIFIC_NAME";

            var list = new List<FunctionModel>();
            using (var cmd = new SqlCommand(fnSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var schema = reader.GetString(0);
                    var name = reader.GetString(1);
                    var retType = reader.IsDBNull(2) ? null : reader.GetString(2);

                    list.Add(new FunctionModel
                    {
                        Schema = schema,
                        Name = name,
                        IsTableValued = string.Equals(retType, "table", StringComparison.OrdinalIgnoreCase),
                        ScalarReturn = retType is null || retType.Equals("table", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : MapSqlType(retType, null, null, null)
                    });
                }
            }

            foreach (var f in list)
            {
                f.Parameters = await ReadFunctionParametersAsync(conn, f.Schema, f.Name, ct);
                if (f.IsTableValued)
                    f.TableColumns = await ReadTvfColumnsAsync(conn, f.Schema, f.Name, ct);
            }

            return list;
        }

        private static async Task<IReadOnlyList<ParameterModel>> ReadFunctionParametersAsync(SqlConnection conn, string schema, string name, CancellationToken ct)
        {
            const string paramSql = @"
SELECT PARAMETER_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_SCHEMA = @schema AND SPECIFIC_NAME = @name
ORDER BY ORDINAL_POSITION";

            using var cmd = new SqlCommand(paramSql, conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@name", name);

            var list = new List<ParameterModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var dataType = reader.GetString(1);
                var len = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var prec = reader.IsDBNull(3) ? (int?)null : reader.GetByte(3);
                var scale = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);

                list.Add(new ParameterModel
                {
                    Name = reader.GetString(0),
                    SqlType = MapSqlType(dataType, len, prec, scale),
                    IsOutput = false,
                    IsNullable = false
                });
            }

            return list;
        }

        private static async Task<IReadOnlyList<ResultColumnModel>> ReadTvfColumnsAsync(SqlConnection conn, string schema, string name, CancellationToken ct)
        {
            // TVF columns are not exposed in INFORMATION_SCHEMA directly; use sys.columns by OBJECT_ID
            const string sql = @"
SELECT c.name, t.name, c.max_length, c.precision, c.scale, c.is_nullable, c.column_id
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(@fullName)
ORDER BY c.column_id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@fullName", $"[{schema}].[{name}]");

            var list = new List<ResultColumnModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new ResultColumnModel
                {
                    Name = reader.GetString(0),
                    SqlType = MapSqlType(reader.GetString(1), reader.GetInt16(2), reader.GetByte(3), reader.GetByte(4)),
                    IsNullable = reader.GetBoolean(5),
                    Ordinal = reader.GetInt32(6)
                });
            }
            return list;
        }

        private static async Task<IReadOnlyList<ViewModel>> ReadViewsAsync(SqlConnection conn, CancellationToken ct)
        {
            const string viewSql = @"
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.VIEWS
ORDER BY TABLE_SCHEMA, TABLE_NAME";

            var views = new List<ViewModel>();
            using (var cmd = new SqlCommand(viewSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    views.Add(new ViewModel
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1)
                    });
                }
            }

            foreach (var v in views)
                v.Columns = await ReadViewColumnsAsync(conn, v.Schema, v.Name, ct);

            return views;
        }

        private static async Task<IReadOnlyList<ResultColumnModel>> ReadViewColumnsAsync(SqlConnection conn, string schema, string name, CancellationToken ct)
        {
            const string colSql = @"
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @name
ORDER BY ORDINAL_POSITION";

            using var cmd = new SqlCommand(colSql, conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@name", name);

            var list = new List<ResultColumnModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new ResultColumnModel
                {
                    Name = reader.GetString(0),
                    SqlType = MapSqlType(reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : reader.GetByte(3),
                        reader.IsDBNull(4) ? null : reader.GetInt32(4)),
                    IsNullable = string.Equals(reader.GetString(5), "YES", StringComparison.OrdinalIgnoreCase),
                    Ordinal = reader.GetInt32(6)
                });
            }
            return list;
        }

        private static SqlType MapSqlType(string dataType, int? len, int? prec, int? scale)
        {
            return dataType.ToLowerInvariant() switch
            {
                "int" => SqlType.Int(),
                "bigint" => SqlType.BigInt(),
                "bit" => SqlType.Bit(),
                "uniqueidentifier" => SqlType.UniqueIdentifier(),
                "datetime2" => SqlType.DateTime2(prec ?? 7),
                "datetimeoffset" => SqlType.DateTimeOffset(prec ?? 7),
                "date" => SqlType.Date(),
                "decimal" => SqlType.Decimal(prec ?? 18, scale ?? 0),
                "numeric" => SqlType.Numeric(prec ?? 18, scale ?? 0),
                "float" => SqlType.Float(),
                "real" => SqlType.Real(),
                "varbinary" => SqlType.VarBinary(len ?? -1),
                "varchar" => SqlType.VarChar(len ?? -1),
                "nvarchar" => SqlType.NVarChar(len ?? -1),
                "nchar" => SqlType.NChar(len ?? 1),
                "xml" => SqlType.Xml(),
                _ => SqlType.VarChar(len ?? -1) // fallback
            };
        }

        private static SqlType ParseSysType(string sysTypeName)
        {
            // sys.sp_describe_first_result_set outputs like: nvarchar(50), decimal(18,2), varbinary(max), datetime2(7)
            var t = sysTypeName.ToLowerInvariant().Trim();
            if (t.StartsWith("nvarchar"))
            {
                var len = ExtractLength(t);
                return SqlType.NVarChar(len ?? -1);
            }
            if (t.StartsWith("varchar"))
            {
                var len = ExtractLength(t);
                return SqlType.VarChar(len ?? -1);
            }
            if (t.StartsWith("varbinary"))
            {
                var len = ExtractLength(t);
                return SqlType.VarBinary(len ?? -1);
            }
            if (t.StartsWith("decimal") || t.StartsWith("numeric"))
            {
                var (p, s) = ExtractPrecisionScale(t);
                return t.StartsWith("decimal") ? SqlType.Decimal(p ?? 18, s ?? 0) : SqlType.Numeric(p ?? 18, s ?? 0);
            }
            if (t.StartsWith("datetime2"))
            {
                var p = ExtractLength(t);
                return SqlType.DateTime2(p ?? 7);
            }
            if (t.StartsWith("datetimeoffset"))
            {
                var p = ExtractLength(t);
                return SqlType.DateTimeOffset(p ?? 7);
            }
            return t switch
            {
                "int" => SqlType.Int(),
                "bigint" => SqlType.BigInt(),
                "bit" => SqlType.Bit(),
                "uniqueidentifier" => SqlType.UniqueIdentifier(),
                "float" => SqlType.Float(),
                "real" => SqlType.Real(),
                "date" => SqlType.Date(),
                "xml" => SqlType.Xml(),
                _ => SqlType.VarChar(-1)
            };

            static int? ExtractLength(string s)
            {
                var start = s.IndexOf('(');
                var end = s.IndexOf(')');
                if (start < 0 || end < 0) return null;
                var inner = s.Substring(start + 1, end - start - 1);
                if (inner.Equals("max", StringComparison.OrdinalIgnoreCase)) return -1;
                return int.TryParse(inner, out var v) ? v : null;
            }

            static (int? p, int? s) ExtractPrecisionScale(string s)
            {
                var start = s.IndexOf('(');
                var end = s.IndexOf(')');
                if (start < 0 || end < 0) return (null, null);
                var inner = s.Substring(start + 1, end - start - 1);
                var parts = inner.Split(',');
                if (parts.Length != 2) return (null, null);
                return (int.TryParse(parts[0], out var p) ? p : null,
                        int.TryParse(parts[1], out var sc) ? sc : null);
            }
        }
    }
}