using EnviroPi.Sensor.Services;
using EnviroPi.Sensor.Workers;
using EnviroPi.Shared.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection("MqttOptions"));

// One MQTT connection shared by everything that publishes
builder.Services.AddSingleton<IMqttPublishService, MqttPublishService>();

builder.Services.AddHostedService<SensorPublisherWorker>();
builder.Services.AddSystemd();

var host = builder.Build();
host.Run();
