using OrderPipeline.Domain.Orders;

namespace OrderPipeline.Domain.Tests.Orders;

public class PlaceOrderCommandTests
{
    private const string ValidBody = """
        {"customerName":"Alice","product":"Headphones","amount":1499,"notes":"Express"}
        """;

    [Fact]
    public void Parse_WithValidBody_ReturnsTheCommand()
    {
        var result = PlaceOrderCommand.Parse(ValidBody);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice", result.Value!.CustomerName);
        Assert.Equal("Headphones", result.Value.Product);
        Assert.Equal(1499m, result.Value.Amount);
        Assert.Equal("Express", result.Value.Notes);
    }

    [Fact]
    public void Parse_WithSurroundingWhitespace_TrimsTheText()
    {
        var result = PlaceOrderCommand.Parse("""
            {"customerName":"  Alice  ","product":"  Headphones  ","amount":1}
            """);

        Assert.Equal("Alice", result.Value!.CustomerName);
        Assert.Equal("Headphones", result.Value.Product);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"customerName\":")]
    [InlineData("[1,2,3]")]
    [InlineData("\"a string\"")]
    public void Parse_WithUnusableBody_ReportsInvalidJson(string? body)
    {
        var result = PlaceOrderCommand.Parse(body);

        Assert.False(result.IsSuccess);
        Assert.Equal("Request body must be valid JSON.", result.Error);
    }

    [Theory]
    [InlineData("""{"product":"p","amount":1}""")]
    [InlineData("""{"customerName":"","product":"p","amount":1}""")]
    [InlineData("""{"customerName":"   ","product":"p","amount":1}""")]
    [InlineData("""{"customerName":null,"product":"p","amount":1}""")]
    [InlineData("""{"customerName":42,"product":"p","amount":1}""")]
    public void Parse_WithoutUsableCustomerName_SaysItIsRequired(string body)
    {
        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("customerName is required.", result.Error);
    }

    [Fact]
    public void Parse_WithOverlongCustomerName_SaysItIsTooLong()
    {
        var body = $$"""{"customerName":"{{new string('a', 201)}}","product":"p","amount":1}""";

        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("customerName must be at most 200 characters.", result.Error);
    }

    [Fact]
    public void Parse_WithCustomerNameAtTheLimit_Succeeds()
    {
        var body = $$"""{"customerName":"{{new string('a', 200)}}","product":"p","amount":1}""";

        var result = PlaceOrderCommand.Parse(body);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("""{"customerName":"c","amount":1}""")]
    [InlineData("""{"customerName":"c","product":"","amount":1}""")]
    [InlineData("""{"customerName":"c","product":"   ","amount":1}""")]
    public void Parse_WithoutUsableProduct_SaysItIsRequired(string body)
    {
        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("product is required.", result.Error);
    }

    [Fact]
    public void Parse_WithOverlongProduct_SaysItIsTooLong()
    {
        var body = $$"""{"customerName":"c","product":"{{new string('p', 201)}}","amount":1}""";

        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("product must be at most 200 characters.", result.Error);
    }

    [Theory]
    [InlineData("""{"customerName":"c","product":"p"}""")]
    [InlineData("""{"customerName":"c","product":"p","amount":null}""")]
    public void Parse_WithoutAmount_SaysItIsRequired(string body)
    {
        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("amount is required.", result.Error);
    }

    [Theory]
    [InlineData("""{"customerName":"c","product":"p","amount":"1499"}""")]
    [InlineData("""{"customerName":"c","product":"p","amount":true}""")]
    [InlineData("""{"customerName":"c","product":"p","amount":{}}""")]
    public void Parse_WithNonNumericAmount_SaysItMustBeANumber(string body)
    {
        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("amount must be a number.", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1499)]
    public void Parse_WithAmountAtOrBelowZero_SaysItMustBePositive(int amount)
    {
        var result = PlaceOrderCommand.Parse($$"""{"customerName":"c","product":"p","amount":{{amount}}}""");

        Assert.Equal("amount must be greater than zero.", result.Error);
    }

    [Fact]
    public void Parse_WithOverlongNotes_SaysTheyAreTooLong()
    {
        var body = $$"""{"customerName":"c","product":"p","amount":1,"notes":"{{new string('n', 1001)}}"}""";

        var result = PlaceOrderCommand.Parse(body);

        Assert.Equal("notes must be at most 1000 characters.", result.Error);
    }

    [Fact]
    public void Parse_WithoutNotes_LeavesThemEmpty()
    {
        var result = PlaceOrderCommand.Parse("""{"customerName":"c","product":"p","amount":1}""");

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value!.Notes);
    }

    [Fact]
    public void Parse_WithAmountAboveTheBusinessLimit_IsStillAccepted()
    {
        var result = PlaceOrderCommand.Parse("""{"customerName":"c","product":"p","amount":9999999}""");

        Assert.True(result.IsSuccess);
    }
}
