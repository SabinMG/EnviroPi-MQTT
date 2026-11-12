using MQTTnet;

namespace EnviroPi.Shared;

public static class MqttConnection
{
    /// <summary>
    /// Connects to the broker, retrying until it succeeds or cancellation is requested,
    /// so services don't crash when started before the broker is reachable.
    /// </summary>
    public static async Task ConnectWithRetryAsync(
        IMqttClient client,
        MqttClientOptions options,
        Action<Exception, int>? onFailure,
        CancellationToken cancellationToken,
        TimeSpan? retryDelay = null)
    {
        var delay = retryDelay ?? TimeSpan.FromSeconds(5);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await client.ConnectAsync(options, cancellationToken);
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                onFailure?.Invoke(ex, attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
