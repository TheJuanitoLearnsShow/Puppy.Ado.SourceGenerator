using System;
using System.Collections.Generic;

namespace Puppy.Ado.SourceGenerator
{
    public sealed class FunctionModel
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsTableValued { get; set; }
        public IReadOnlyList<ParameterModel> Parameters { get; set; } = Array.Empty<ParameterModel>();
        public IReadOnlyList<ResultColumnModel> TableColumns { get; set; } = Array.Empty<ResultColumnModel>(); // if TVF
        public SqlType? ScalarReturn { get; set; } // if scalar UDF
        public string FullName => $"[{Schema}].[{Name}]";
        public string ClrName => $"{Schema}_{Name}";
    }
}