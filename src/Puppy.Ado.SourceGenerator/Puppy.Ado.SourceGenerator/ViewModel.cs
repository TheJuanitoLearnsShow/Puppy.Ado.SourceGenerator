using System;
using System.Collections.Generic;

namespace Puppy.Ado.SourceGenerator
{
    public sealed class ViewModel
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public IReadOnlyList<ResultColumnModel> Columns { get; set; } = Array.Empty<ResultColumnModel>();
        public string FullName => $"[{Schema}].[{Name}]";
        public string ClrName => $"{Schema}_{Name}";
    }
}