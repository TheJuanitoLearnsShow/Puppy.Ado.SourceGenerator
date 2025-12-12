// See https://aka.ms/new-console-template for more information


using Puppy.Ado.SourceGenerator;

var connStr = GetConnectionString();
var outputFolder = GetOutputFolder();


var reader = new SqlServerSchemaReaderSync(connStr);
var model = reader.Read();

foreach (var v in model.Views)
{
    OutputFile($"{v.ClrName}.View.g.cs", SqlModelTextMapper.EmitView(v));

}

foreach (var f in model.Functions)
    spc.AddSource($"{f.ClrName}.Function.g.cs", SqlModelTextMapper.EmitFunction(f));

foreach (var p in model.StoredProcedures)
    spc.AddSource($"{p.ClrName}.Procedure.g.cs", SqlModelTextMapper.EmitProcedure(p));

string GetConnectionString()
{
    var connStrFromParams = args.FirstOrDefault();
    return connStrFromParams ?? Environment.GetEnvironmentVariable("SQL_Connection_String");
}

string GetOutputFolder()
{
    var folder = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
}

void OutputFile(string fileName, string emitView)
{
    throw new NotImplementedException();
}