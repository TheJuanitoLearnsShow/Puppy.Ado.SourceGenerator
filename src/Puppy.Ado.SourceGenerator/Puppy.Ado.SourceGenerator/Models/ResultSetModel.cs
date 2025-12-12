using System;
using System.Collections.Generic;

namespace Puppy.Ado.SourceGenerator.Models
{
    public sealed class ResultSetModel
    {
        public int Index { get; set; }
        public IReadOnlyList<ResultColumnModel> Columns { get; set; } = Array.Empty<ResultColumnModel>();
    }
}