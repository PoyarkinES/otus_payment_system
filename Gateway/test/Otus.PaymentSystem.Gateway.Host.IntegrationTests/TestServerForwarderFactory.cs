using Yarp.ReverseProxy.Forwarder;

namespace Otus.PaymentSystem.Gateway.Host.IntegrationTests;

public sealed class TestServerForwarderFactory : IForwarderHttpClientFactory
{
    private readonly HttpMessageInvoker _invoker;

    public TestServerForwarderFactory(HttpMessageHandler handler)
    {
        _invoker = new HttpMessageInvoker(handler, disposeHandler: false);
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => _invoker;
}
