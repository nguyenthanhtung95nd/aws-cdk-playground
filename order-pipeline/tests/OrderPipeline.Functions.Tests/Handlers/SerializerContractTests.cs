using Amazon.Lambda.Serialization.SystemTextJson;

namespace OrderPipeline.Functions.Tests.Handlers;

public class SerializerContractTests
{
    [Fact]
    public void Functions_Always_DeclareTheSerializerThatSpellsFieldsTheWayTheClientReadsThem()
    {
        Assert.IsType<CamelCaseLambdaJsonSerializer>(HttpResultWire.FromAssemblyAttribute());
    }
}
