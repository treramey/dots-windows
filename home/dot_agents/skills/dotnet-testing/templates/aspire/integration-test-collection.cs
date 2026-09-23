namespace MyApp.Tests.Integration.Infrastructure;

/// <summary>
/// Integration test collection definition — uses Collection Fixture so AspireAppFixture
/// is shared across every test class, avoiding redundant container startup.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<AspireAppFixture>
{
    /// <summary>
    /// Name of the integration-test collection.
    /// </summary>
    public const string Name = "Integration Tests";

    // This class needs no implementation — it exists to define a Collection Fixture.
    // Any test class marked [Collection("Integration Tests")] will share the same
    // AspireAppFixture instance.
}

// Usage:
// [Collection(IntegrationTestCollection.Name)]
// public class MyControllerTests : IntegrationTestBase
// {
//     public MyControllerTests(AspireAppFixture fixture) : base(fixture) { }
// }
