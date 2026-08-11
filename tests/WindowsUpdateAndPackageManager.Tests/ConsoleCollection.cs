using Xunit;

[CollectionDefinition("Console", DisableParallelization = true)]
public class ConsoleCollection : ICollectionFixture<ConsoleCollection>
{
}
