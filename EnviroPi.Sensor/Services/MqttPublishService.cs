using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using EnviroPi.Shared;
using EnviroPi.Shared.Models;

namespace EnviroPi.Sensor.Services;

public sealed class MqttPublishService : IMqttPublishService, IAsyncDisposable
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttPublishService> _logger;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public MqttPublishService(IOptions<MqttOptions> options, ILogger<MqttPublishService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new MqttClientFactory().CreateMqttClient();
        _clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Server, _options.Port)
            .WithClientId($"{_options.ClientIdPrefix}_{Environment.MachineName}")
            .WithCleanSession()
            .Build();
    }

    public Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default)
        => PublishAsync(_options.Topic, payload, cancellationToken);

    public async Task PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        string json = JsonSerializer.Serialize(payload);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
        _logger.LogInformation("Published to [{Topic}]: {Json}", topic, json);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected) return;

        // Only one caller connects; concurrent publishers wait for it.
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected) return;

            await MqttConnection.ConnectWithRetryAsync(_client, _clientOptions,
                (ex, attempt) => _logger.LogWarning("MQTT connect attempt {Attempt} failed: {Message}. Retrying...", attempt, ex.Message),
                cancellationToken);
            _logger.LogInformation("Connected to MQTT broker at {Server}:{Port}", _options.Server, _options.Port);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while disconnecting from MQTT broker.");
        }
        _client.Dispose();
        _connectLock.Dispose();
    }
}
