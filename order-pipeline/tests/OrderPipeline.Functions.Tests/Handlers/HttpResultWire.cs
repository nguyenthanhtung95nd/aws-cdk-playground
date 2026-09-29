using System.Reflection;
using System.Text.Json;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using OrderPipeline.Functions.Handlers;

namespace OrderPipeline.Functions.Tests.Handlers;

public static class HttpResultWire
{
    // The generated handlers resolve their serializer from the assembly attribute, so naming one
    // here instead would let that attribute be deleted without a single test noticing, and every
    // field the client reads would silently change case.
    private static readonly ILambdaSerializer Configured = FromAssemblyAttribute();

    public static ILambdaSerializer FromAssemblyAttribute()
    {
        var assembly = typeof(OrderApi).Assembly;
        var declared = assembly.GetCustomAttribute<LambdaSerializerAttribute>()
            ?? throw new InvalidOperationException(
                $"'{assembly.GetName().Name}' declares no [LambdaSerializer], so Lambda would "
                + "spell every response field in PascalCase and the client would not decode it.");

        return (ILambdaSerializer)Activator.CreateInstance(declared.SerializerType)!;
    }

    public static JsonElement Of(IHttpResult result)
    {
        using var payload = result.Serialize(new HttpResultSerializationOptions
        {
            Format = HttpResultSerializationOptions.ProtocolFormat.HttpApi,
            Version = HttpResultSerializationOptions.ProtocolVersion.V2,
            Serializer = Configured,
        });

        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
