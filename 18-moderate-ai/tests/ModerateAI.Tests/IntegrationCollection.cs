using Xunit;

namespace ModerateAI.Tests;

[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>;
