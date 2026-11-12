using EnviroPi.Shared;
using MQTTnet;
using NSubstitute;

namespace EnviroPi.Tests.Shared;

public class MqttConnectionTests
{
    private static readonly MqttClientOptions Options = new MqttClientOptionsBuilder()
        .WithTcpServer("localhost", 1883)
        .Build();

    private static readonly TimeSpan NoDelay = TimeSpan.FromMilliseconds(1);

    private static Task<MqttClientConnectResult> Fail() =>
        Task.FromException<MqttClientConnectResult>(new InvalidOperationException("broker down"));

    private static Task<MqttClientConnectResult> Succeed() =>
        Task.FromResult<MqttClientConnectResult>(null!);

    [Fact]
    public async Task Connects_on_first_attempt_without_reporting_failure()
    {
        var client = Substitute.For<IMqttClient>();
        client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>()).Returns(_ => Succeed());
        var failures = 0;

        await MqttConnection.ConnectWithRetryAsync(client, Options, (_, _) => failures++, CancellationToken.None, NoDelay);

        await client.Received(1).ConnectAsync(Options, Arg.Any<CancellationToken>());
        Assert.Equal(0, failures);
    }

    [Fact]
    public async Task Retries_until_connect_succeeds_and_reports_each_failed_attempt()
    {
        var client = Substitute.For<IMqttClient>();
        client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Fail(), _ => Fail(), _ => Succeed());
        var attempts = new List<int>();

        await MqttConnection.ConnectWithRetryAsync(client, Options, (_, attempt) => attempts.Add(attempt), CancellationToken.None, NoDelay);

        await client.Received(3).ConnectAsync(Options, Arg.Any<CancellationToken>());
        Assert.Equal([1, 2], attempts);
    }

    [Fact]
    public async Task Works_without_a_failure_callback()
    {
        var client = Substitute.For<IMqttClient>();
        client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Fail(), _ => Succeed());

        await MqttConnection.ConnectWithRetryAsync(client, Options, onFailure: null, CancellationToken.None, NoDelay);

        await client.Received(2).ConnectAsync(Options, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stops_retrying_when_cancelled()
    {
        var client = Substitute.For<IMqttClient>();
        client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>()).Returns(_ => Fail());
        using var cts = new CancellationTokenSource();

        // Cancel after the first failure; the retry delay should then throw instead of looping forever.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MqttConnection.ConnectWithRetryAsync(client, Options, (_, _) => cts.Cancel(), cts.Token, TimeSpan.FromSeconds(30)));

        await client.Received(1).ConnectAsync(Options, Arg.Any<CancellationToken>());
    }
}
