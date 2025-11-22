namespace Puppy.Ado.SourceGenerator
{
    public sealed record TableTypeModel
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public IReadOnlyList<ResultColumnModel> Columns { get; set; } = Array.Empty<ResultColumnModel>();
        public string FullName => $"[{Schema}].[{Name}]";
    }
}