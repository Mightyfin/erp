using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Infrastructure;
using Xunit;
using Mightyfin.Erp.Hrm.Domain.Entities;
using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class NatsBrokerFactAttribute : FactAttribute
{
    public NatsBrokerFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVENTBUS_ACL_TEST_URL")))
            Skip = "Requires the isolated event-permission broker runner.";
    }
}

public sealed class NatsEventCredentialsTests
{
    [NatsBrokerFact]
    public async Task RestrictedPublisherStoresOneEvent()
    {
        var url = Environment.GetEnvironmentVariable("EVENTBUS_ACL_TEST_URL");
        Assert.False(string.IsNullOrEmpty(url));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var bootstrap = new NatsClient(NatsOpts.Default with
        {
            Url = url, AuthOpts = NatsAuthOpts.Default with
            { Username = "bootstrap", Password = Environment.GetEnvironmentVariable("EVENTBUS_ACL_TEST_BOOTSTRAP_PASSWORD") },
        });
        var js = bootstrap.CreateJetStreamContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["HRM:NatsUrl"] = url, ["HRM:NatsUser"] = "hrm-publisher",
            ["HRM:NatsPassword"] = Environment.GetEnvironmentVariable("EVENTBUS_ACL_TEST_PUBLISHER_PASSWORD"),
        }).Build();
        await using var publisher = new NatsHrmEventPublisher(config);
        await Assert.ThrowsAnyAsync<Exception>(() => publisher.EnsureStreamAsync(ct));
        await js.CreateStreamAsync(new StreamConfig("HRM_EVENTS", ["mightyfin.hrm.>"]) { Storage = StreamConfigStorage.Memory }, ct);
        try
        {
            await publisher.EnsureStreamAsync(ct);
            var row = new OutboxMessage { PublicId = "synthetic-hrm-acl", EventType = "hrm.synthetic", Environment = "sandbox", SubjectId = "synthetic", CorrelationId = "synthetic", PayloadJson = "{}" };
            await publisher.PublishAsync(row, ct);
            await publisher.PublishAsync(row, ct);
            var stream = await js.GetStreamAsync("HRM_EVENTS", cancellationToken: ct);
            Assert.Equal(1L, stream.Info.State.Messages);
        }
        finally { await js.DeleteStreamAsync("HRM_EVENTS", ct); }
    }

    [Theory]
    [InlineData("synthetic", null, null, true)]
    [InlineData(null, "hrm-publisher", "synthetic", true)]
    [InlineData("synthetic", "hrm-publisher", "synthetic", false)]
    [InlineData(null, "hrm-publisher", null, false)]
    [InlineData(null, null, "synthetic", false)]
    [InlineData(null, null, null, false)]
    public void CredentialsFailClosed(string? token, string? user, string? password, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["HRM:NatsToken"] = token, ["HRM:NatsUser"] = user, ["HRM:NatsPassword"] = password,
        }).Build();
        if (!valid)
        {
            Assert.Throws<InvalidOperationException>(() => NatsHrmEventPublisher.EventCredentials(config));
            return;
        }
        var opts = NatsHrmEventPublisher.EventCredentials(config);
        Assert.Equal(token, opts.Token);
        Assert.Equal(user, opts.Username);
        Assert.Equal(password, opts.Password);
    }
}
