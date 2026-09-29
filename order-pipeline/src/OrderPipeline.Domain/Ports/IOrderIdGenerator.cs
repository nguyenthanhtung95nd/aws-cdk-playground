namespace OrderPipeline.Domain.Ports;

public interface IOrderIdGenerator
{
    string Next();
}
