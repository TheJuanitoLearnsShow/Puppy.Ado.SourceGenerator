namespace AdoGenerator.Tests;

public class GeneratedCodeTests
{
    private const string ConnectionName = "AdoGenerator_SampleDb";
    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionName) ?? 
               $"Data Source=.\\sqlExpress;Database=AutomatedTESTS_{ConnectionName};Integrated Security=True;TrustServerCertificate=True;";
    }

    [Fact]
    public async Task Test_Sp()
    {
        var connStr = GetConnectionString();
        var client = new dbo_spEnrollStudent_ProcClient(connStr, true);
        var request = new dbo_spEnrollStudent_Input()
        {
            Age = 5, 
            ClassesToEnroll =
            [
                ("Math", 1), 
                ("Science", 2) 
            ],
            FirstName = "John",
            LastName = "Doe"
        };
        var results = await client.ExecuteAsync(request);
        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }
}