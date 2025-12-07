using System;
using System.Collections.Generic;

namespace Puppy.Ado.SourceGenerator
{
    public sealed class DatabaseModel
    {
        public IReadOnlyList<StoredProcedureModel> StoredProcedures { get; set; } = Array.Empty<StoredProcedureModel>();
        public IReadOnlyList<FunctionModel> Functions { get; set; } = Array.Empty<FunctionModel>();
        public IReadOnlyList<ViewModel> Views { get; set; } = Array.Empty<ViewModel>();
        public IReadOnlyList<TableTypeModel> TableTypes { get; set; } = Array.Empty<TableTypeModel>();
    }

    public abstract record SqlType(string Name, bool HasLength, int? Length, int? Precision, int? Scale)
    {
        public static SqlType Int() => new Simple("int");
        public static SqlType BigInt() => new Simple("bigint");
        public static SqlType Bit() => new Simple("bit");
        public static SqlType UniqueIdentifier() => new Simple("uniqueidentifier");
        public static SqlType DateTime2(int precision = 7) => new PScale("datetime2", precision);
        public static SqlType Date() => new Simple("date");
        public static SqlType DateTimeOffset(int precision = 7) => new PScale("datetimeoffset", precision);
        public static SqlType Decimal(int precision, int scale) => new PScale("decimal", precision, scale);
        public static SqlType Numeric(int precision, int scale) => new PScale("numeric", precision, scale);
        public static SqlType Float() => new Simple("float");
        public static SqlType Real() => new Simple("real");
        public static SqlType VarBinary(int length) => new Len("varbinary", length);
        public static SqlType VarChar(int length) => new Len("varchar", length);
        public static SqlType NVarChar(int length) => new Len("nvarchar", length);
        public static SqlType NChar(int length) => new Len("nchar", length);
        public static SqlType Xml() => new Simple("xml");
        public static SqlType Structured(string fullTypeName) => new StructuredType(fullTypeName);

        private sealed record Simple(string N) : SqlType(N, false, null, null, null);
        private sealed record Len(string N, int L) : SqlType(N, true, L, null, null);
        private sealed record PScale(string N, int P, int? S = null) : SqlType(N, false, null, P, S);
        private sealed record StructuredType(string TypeName) : SqlType(TypeName, false, null, null, null);
    }
}