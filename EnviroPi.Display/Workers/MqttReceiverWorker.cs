using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using EnviroPi.Display.Services;
using EnviroPi.Shared;
using EnviroPi.Shared.Models;

namespace EnviroPi.Display.Workers;

public class MqttReceiverWorker : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly IDisplayService _displayService;
    private readonly ILogger<MqttReceiverWorker> _logger;

    public MqttReceiverWorker(
        IOptions<MqttOptions> options,
        IDisplayService displayService,
        ILogger<MqttReceiverWorker> logger)
    {
        _options = options.Value;
        _displayService = displayService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        using var mqttClient = factory.CreateMqttClient();

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            string rawPayload = e.ApplicationMessage.ConvertPayloadToString();
            _logger.LogInformation("Received MQTT payload: {Payload}", rawPayload);

            try
            {
                var data = JsonSerializer.Deserialize<TelemetryPayload>(rawPayload);
                if (data != null)
                {
                    _displayService.RenderTelemetry(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse incoming payload.");
            }

            return Task.CompletedTask;
        };

        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Server, _options.Port)
            .WithClientId($"{_options.ClientIdPrefix}_{Guid.NewGuid().ToString()[..6]}")
            .WithCleanSession()
            .Build();

        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(_options.Topic))
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!mqttClient.IsConnected)
            {
                await MqttConnection.ConnectWithRetryAsync(mqttClient, clientOptions,
                    (ex, attempt) => _logger.LogWarning("MQTT connect attempt {Attempt} failed: {Message}. Retrying...", attempt, ex.Message),
                    stoppingToken);

                // Clean session => subscriptions must be re-established after every (re)connect
                await mqttClient.SubscribeAsync(subscribeOptions, stoppingToken);
                _logger.LogInformation("Connected to {Server}:{Port} and subscribed to MQTT topic [{Topic}]",
                    _options.Server, _options.Port, _options.Topic);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
