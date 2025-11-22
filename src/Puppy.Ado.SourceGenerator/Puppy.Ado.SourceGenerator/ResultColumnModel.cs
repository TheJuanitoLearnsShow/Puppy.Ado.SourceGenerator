namespace Puppy.Ado.SourceGenerator
{
    public sealed class ResultColumnModel
    {
        public string Name { get; set; } = "";
        public SqlType SqlType { get; set; } = SqlType.VarChar(1);
        public bool IsNullable { get; set; }
        public int? Ordinal { get; set; }
    }
}