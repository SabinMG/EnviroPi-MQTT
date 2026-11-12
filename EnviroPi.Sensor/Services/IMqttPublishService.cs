namespace EnviroPi.Sensor.Services;

/// <summary>
/// Publishes messages to the MQTT broker configured in <c>MqttOptions</c>.
/// Connection management (connect, retry, reconnect) is handled internally.
/// </summary>
public interface IMqttPublishService
{
    /// <summary>Serializes <paramref name="payload"/> to JSON and publishes it to the configured topic.</summary>
    Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default);

    /// <summary>Serializes <paramref name="payload"/> to JSON and publishes it to <paramref name="topic"/>.</summary>
    Task PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken = default);
}
