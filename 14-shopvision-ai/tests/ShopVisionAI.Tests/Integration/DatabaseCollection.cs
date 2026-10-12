namespace ShopVisionAI.Tests.Integration;

[CollectionDefinition("database", DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
