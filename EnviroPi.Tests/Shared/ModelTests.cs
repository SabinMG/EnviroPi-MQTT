using System.Text.Json;
using EnviroPi.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace EnviroPi.Tests.Shared;

public class TelemetryPayloadTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new TelemetryPayload
        {
            DeviceId = "pi-01",
            Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            TemperatureCelsius = 21.5,
            HumidityPercent = 48.3,
            Co2Ppm = 845
        };

        var copy = JsonSerializer.Deserialize<TelemetryPayload>(JsonSerializer.Serialize(original))!;

        Assert.Equal(original.DeviceId, copy.DeviceId);
        Assert.Equal(original.Timestamp, copy.Timestamp);
        Assert.Equal(original.TemperatureCelsius, copy.TemperatureCelsius);
        Assert.Equal(original.HumidityPercent, copy.HumidityPercent);
        Assert.Equal(original.Co2Ppm, copy.Co2Ppm);
    }

    [Fact]
    public void Wire_format_uses_pascal_case_property_names()
    {
        // The Display service deserializes with default (case-sensitive) options,
        // so this is the exact message shape the Sensor must keep sending.
        const string json = """
            {"DeviceId":"pi-01","Timestamp":"2026-01-02T03:04:05Z","TemperatureCelsius":21.5,"HumidityPercent":48.3}
            """;

        var payload = JsonSerializer.Deserialize<TelemetryPayload>(json)!;

        Assert.Equal("pi-01", payload.DeviceId);
        Assert.Equal(21.5, payload.TemperatureCelsius);
        Assert.Equal(48.3, payload.HumidityPercent);
    }

    [Fact]
    public void Co2_is_sent_as_Co2Ppm()
    {
        const string json = """
            {"DeviceId":"pi-01","Timestamp":"2026-01-02T03:04:05Z","TemperatureCelsius":21.5,"HumidityPercent":48.3,"Co2Ppm":1234}
            """;

        Assert.Equal(1234, JsonSerializer.Deserialize<TelemetryPayload>(json)!.Co2Ppm);
    }

    [Fact]
    public void Messages_without_co2_from_older_publishers_still_parse()
    {
        // Payload shape published before the CO2 sensor was added.
        const string json = """
            {"DeviceId":"pi-01","Timestamp":"2026-01-02T03:04:05Z","TemperatureCelsius":21.5,"HumidityPercent":48.3}
            """;

        var payload = JsonSerializer.Deserialize<TelemetryPayload>(json)!;

        Assert.Null(payload.Co2Ppm);
        Assert.Equal(21.5, payload.TemperatureCelsius);
    }
}

public class MqttOptionsTests
{
    [Fact]
    public void Has_sensible_defaults()
    {
        var options = new MqttOptions();

        Assert.Equal("localhost", options.Server);
        Assert.Equal(1883, options.Port);
        Assert.Equal("pi/environment/telemetry", options.Topic);
    }

    [Fact]
    public void Binds_from_configuration_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MqttOptions:Server"] = "broker.lan",
                ["MqttOptions:Port"] = "8883",
                ["MqttOptions:ClientIdPrefix"] = "Test",
                ["MqttOptions:Topic"] = "custom/topic"
            })
            .Build();

        var options = config.GetSection("MqttOptions").Get<MqttOptions>()!;

        Assert.Equal("broker.lan", options.Server);
        Assert.Equal(8883, options.Port);
        Assert.Equal("Test", options.ClientIdPrefix);
        Assert.Equal("custom/topic", options.Topic);
    }

    [Fact]
    public void Sensor_and_display_appsettings_point_at_the_same_broker_and_topic()
    {
        var sensor = LoadOptions("EnviroPi.Sensor");
        var display = LoadOptions("EnviroPi.Display");

        Assert.Equal(sensor.Server, display.Server);
        Assert.Equal(sensor.Port, display.Port);
        Assert.Equal(sensor.Topic, display.Topic);
        Assert.NotEqual(sensor.ClientIdPrefix, display.ClientIdPrefix);
    }

    private static MqttOptions LoadOptions(string project)
    {
        var path = Path.Combine(FindRepoRoot(), project, "appsettings.json");
        return new ConfigurationBuilder().AddJsonFile(path).Build()
            .GetSection("MqttOptions").Get<MqttOptions>()!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EnviroPi.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not find EnviroPi.sln");
    }
}
