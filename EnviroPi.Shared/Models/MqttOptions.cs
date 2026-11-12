namespace EnviroPi.Shared.Models;

public class MqttOptions
{
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientIdPrefix { get; set; } = "PiNode";
    public string Topic { get; set; } = "pi/environment/telemetry";
}
