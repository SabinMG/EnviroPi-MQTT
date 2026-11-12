using System.Device.I2c;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using EnviroPi.Sensor.Services;
using EnviroPi.Shared.Models;

namespace EnviroPi.Sensor.Workers;

public class SensorPublisherWorker : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);

    private readonly IMqttPublishService _publisher;
    private readonly ILogger<SensorPublisherWorker> _logger;
    private Bme280? _bme280;

    public SensorPublisherWorker(IMqttPublishService publisher, ILogger<SensorPublisherWorker> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Initialize Hardware (I2C Bus 1, Address 0x76)
        try
        {
            var i2cSettings = new I2cConnectionSettings(busId: 1, deviceAddress: Bme280.DefaultI2cAddress);
            var i2cDevice = I2cDevice.Create(i2cSettings);
            _bme280 = new Bme280(i2cDevice);
            _logger.LogInformation("Hardware BME280 sensor connected over I2C.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to I2C sensor. Running with default dummy data.");
        }

        // 2. Sensor Loop
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
        double temp = 20.0;
        double hum = 50.0;

        if (_bme280 != null)
        {
            _bme280.SetPowerMode(Bmx280PowerMode.Forced);
            await Task.Delay(100, stoppingToken);
            var readResult = await _bme280.ReadAsync();
            temp = readResult.Temperature?.DegreesCelsius ?? 0.0;
            hum = readResult.Humidity?.Percent ?? 0.0;
        }

        return new TelemetryPayload
        {
            DeviceId = Environment.MachineName,
            Timestamp = DateTime.UtcNow,
            TemperatureCelsius = Math.Round(temp, 1),
            HumidityPercent = Math.Round(hum, 1)
        };
    }

    public override void Dispose()
    {
        _bme280?.Dispose();
        base.Dispose();
    }
}
