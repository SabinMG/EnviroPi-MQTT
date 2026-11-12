using EnviroPi.Sensor.Services;
using EnviroPi.Sensor.Services.Sensors;
using EnviroPi.Shared.Models;

namespace EnviroPi.Sensor.Workers;

public class SensorPublisherWorker : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);

    // Published when no BME280 is connected, so the pipeline can be tested without hardware.
    private static readonly ClimateReading DummyClimate = new(TemperatureCelsius: 20.0, HumidityPercent: 50.0);

    private readonly IMqttPublishService _publisher;
    private readonly IClimateSensor _climateSensor;
    private readonly ICo2Sensor _co2Sensor;
    private readonly ILogger<SensorPublisherWorker> _logger;

    public SensorPublisherWorker(IMqttPublishService publisher, IClimateSensor climateSensor, ICo2Sensor co2Sensor,
        ILogger<SensorPublisherWorker> logger)
    {
        _publisher = publisher;
        _climateSensor = climateSensor;
        _co2Sensor = co2Sensor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var payload = await ReadTelemetryAsync(stoppingToken);
                await _publisher.PublishAsync(payload, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during sensor reading or publish cycle.");
            }

            await Task.Delay(PublishInterval, stoppingToken);
        }
    }

    private async Task<TelemetryPayload> ReadTelemetryAsync(CancellationToken stoppingToken)
    {
        var climate = await ReadClimateAsync(stoppingToken) ?? DummyClimate;

        return new TelemetryPayload
        {
            DeviceId = Environment.MachineName,
            Timestamp = DateTime.UtcNow,
            TemperatureCelsius = Math.Round(climate.TemperatureCelsius, 1),
            HumidityPercent = Math.Round(climate.HumidityPercent, 1),
            Co2Ppm = await ReadCo2Async(stoppingToken)
        };
    }

    // Each sensor is read on its own, so a problem with one never stops the others from being published.
    private async Task<ClimateReading?> ReadClimateAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await _climateSensor.ReadAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Temperature/humidity reading failed; publishing default values.");
            return null;
        }
    }

    private async Task<double?> ReadCo2Async(CancellationToken stoppingToken)
    {
        try
        {
            return await _co2Sensor.ReadCo2PpmAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "CO2 reading failed; publishing without CO2.");
            return null;
        }
    }
}
