using System;
using System.Collections.Generic;

namespace Puppy.Ado.SourceGenerator
{
    public sealed class StoredProcedureModel
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public IReadOnlyList<ParameterModel> Parameters { get; set; } = Array.Empty<ParameterModel>();
        public IReadOnlyList<ResultSetModel> ResultSets { get; set; } = Array.Empty<ResultSetModel>();
        public string FullName => $"[{Schema}].[{Name}]";
        public string ClrName => $"{Schema}_{Name}";
    }
}