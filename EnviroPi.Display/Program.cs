using EnviroPi.Display.Services;
using EnviroPi.Display.Workers;
using EnviroPi.Shared.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection("MqttOptions"));

// Register the display as a Singleton (one I2C connection shared by everything that renders)
builder.Services.AddSingleton<IDisplayService, OledDisplayService>();

// Register MQTT Receiver Worker
builder.Services.AddHostedService<MqttReceiverWorker>();

builder.Services.AddSystemd();

var host = builder.Build();
host.Run();
