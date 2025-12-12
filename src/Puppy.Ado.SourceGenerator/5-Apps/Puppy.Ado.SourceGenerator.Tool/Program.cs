// See https://aka.ms/new-console-template for more information


using Puppy.Ado.SourceGenerator;

var connStr = GetConnectionString();
var outputFolder = GetOutputFolder();


var reader = new SqlServerSchemaReaderSync(connStr);
var model = reader.Read();

var writeTasks = model.Views.Select(v => OutputFile($"{v.ClrName}.View.g.cs", SqlModelTextMapper.EmitView(v)))
    .Concat(model.Functions.Select(f => OutputFile($"{f.ClrName}.Function.g.cs", SqlModelTextMapper.EmitFunction(f))))
    .Concat(model.StoredProcedures.Select(p => OutputFile($"{p.ClrName}.Procedure.g.cs", SqlModelTextMapper.EmitProcedure(p))));

await Task.WhenAll(writeTasks);

string GetConnectionString()
{
    var connStrFromParams = args.FirstOrDefault();
    return connStrFromParams ?? Environment.GetEnvironmentVariable("SQL_Connection_String");
}

string GetOutputFolder()
{
    var folder = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
    return folder ?? Directory.GetCurrentDirectory();
}

Task OutputFile(string fileName, string emitView)
{
    var finalePath = Path.Combine(outputFolder, fileName);
    return File.WriteAllTextAsync(finalePath, emitView);
}