@REM this line does not need to run multiple times. It is only used to ensure the sqlpackage tool is installed and up-to-date
@REM dotnet tool install -g microsoft.sqlpackage

dotnet build /p:NetCoreBuild=true 
@REM /p:Configuration=Release

:: The SDK DB build produces PodcastsManagerSDKDb.dacpac; use that filename
SqlPackage /Action:Publish /SourceFile:".\bin\debug\SampleDb.dacpac" /TargetConnectionString:"Data Source=.\sqlExpress;Database=AutomatedTESTS_AdoGenerator_SampleDb;Integrated Security=True;TrustServerCertificate=True;" /p:ScriptDatabaseCompatibility=True /p:BlockOnPossibleDataLoss=False
