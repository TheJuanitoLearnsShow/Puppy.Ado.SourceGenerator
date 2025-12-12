using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Puppy.Ado.SourceGenerator
{
    [Generator]
    public sealed class DbGenFromInfoSchema : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var options = context.AnalyzerConfigOptionsProvider.Select(
                (p, _) => new GeneratorOptions(p));
            var modelProvider = options.Select( (o, ct) =>
            {
                if (!o.EnableLiveSchema || string.IsNullOrWhiteSpace(o.ConnectionString))
                    return null;

                var reader = new SqlServerSchemaReaderSync(o.ConnectionString!);
                return reader.Read();
            });

            context.RegisterSourceOutput(modelProvider, (spc, model) =>
            {
                if (model is null) return;

                foreach (var v in model.Views)
                    spc.AddSource($"{v.ClrName}.View.g.cs", SqlModelTextMapper.EmitView(v));

                foreach (var f in model.Functions)
                    spc.AddSource($"{f.ClrName}.Function.g.cs", SqlModelTextMapper.EmitFunction(f));

                foreach (var p in model.StoredProcedures)
                    spc.AddSource($"{p.ClrName}.Procedure.g.cs", SqlModelTextMapper.EmitProcedure(p));
            });
        }

        

        private sealed class GeneratorOptions
        {
            public bool EnableLiveSchema { get; }
            public string? ConnectionString { get; }

            public GeneratorOptions(AnalyzerConfigOptionsProvider provider)
            {
                var global = provider.GlobalOptions;
                global.TryGetValue("build_property.DbGen_EnableLiveSchema", out var enable);
                global.TryGetValue("build_property.DbGen_ConnectionString", out var conn);
                EnableLiveSchema = bool.TryParse(enable, out var b) && b;
                ConnectionString = conn?.Replace('|', ';'); 
            }
        }

    }
}