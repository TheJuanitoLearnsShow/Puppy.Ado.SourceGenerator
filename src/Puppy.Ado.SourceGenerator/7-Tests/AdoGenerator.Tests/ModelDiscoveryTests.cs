using Puppy.Ado.SourceGenerator;

namespace AdoGenerator.Tests;

public class ModelDiscoveryTests
{
    private const string ConnectionName = "AdoGenerator_SampleDb";
    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionName) ?? 
               $"Data Source=.\\sqlExpress;Database=AutomatedTESTS_{ConnectionName};Integrated Security=True;TrustServerCertificate=True;";
    }
    [Fact]
    public async Task Test_Model()
    {
        var connStr = GetConnectionString();
        var reader = new SqlServerSchemaReader(connStr);
        var model = await reader.ReadAsync();
        Assert.NotNull(model);
    }
}