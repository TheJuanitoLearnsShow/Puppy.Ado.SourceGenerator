namespace Puppy.Ado.SourceGenerator
{
    public sealed class ResultSetModel
    {
        public int Index { get; set; }
        public IReadOnlyList<ResultColumnModel> Columns { get; set; } = Array.Empty<ResultColumnModel>();
    }
}