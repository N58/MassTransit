namespace MassTransit.Azure.ServiceBus.Core.Tests
{
    using System;
    using System.Threading.Tasks;
    using MassTransit.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;
    using Registration;
    using TestFramework;
    using TestFramework.Messages;


    [TestFixture]
    public class Connector_Specs :
        BusTestFixture
    {
        [Test]
        public async Task Should_connect_subscription_endpoint()
        {
            ServiceBusTokenProviderSettings settings = new TestAzureServiceBusAccountSettings();

            var serviceUri = AzureServiceBusEndpointUriCreator.Create(Configuration.ServiceNamespace);

            var provider = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(LoggerFactory)
                .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
                .AddMassTransit(x =>
                {
                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        cfg.Host(serviceUri, h =>
                        {
                            h.SharedAccessSignature(s =>
                            {
                                s.KeyName = settings.KeyName;
                                s.SharedAccessKey = settings.SharedAccessKey;
                                s.TokenTimeToLive = settings.TokenTimeToLive;
                                s.TokenScope = settings.TokenScope;
                            });
                        });
                    });
                }).BuildServiceProvider(true);

            var depot = provider.GetRequiredService<IBusDepot>();

            await depot.Start(TestCancellationToken);
            try
            {
                var bus = provider.GetRequiredService<IBus>();

                var connector = provider.GetRequiredService<ISubscriptionEndpointConnector>();

                Task<ConsumeContext<PingMessage>> handled = null;
                var handle = connector.ConnectSubscriptionEndpoint<PingMessage>("my-sub", e =>
                {
                    handled = Handled<PingMessage>(e);
                });

                await handle.Ready;

                await bus.Publish(new PingMessage());

                await handled;
            }
            finally
            {
                await depot.Stop(TimeSpan.FromSeconds(30));
            }
        }

        public Connector_Specs()
            : base(new InMemoryTestHarness())
        {
        }
    }
}
