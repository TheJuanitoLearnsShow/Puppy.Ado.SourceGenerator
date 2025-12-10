namespace Puppy.Ado.SourceGenerator
{
    public sealed class ParameterModel
    {
        public string Name { get; set; } = "";
        public SqlType SqlType { get; set; } = SqlType.VarChar(1);
        public bool IsOutput { get; set; }
        public bool IsNullable { get; set; }
        public bool IsTableValued { get; set; }
        public string? TableTypeFullName { get; set; }

        public string ClrName { get; set; }
    }
}