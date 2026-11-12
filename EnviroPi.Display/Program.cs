using EnviroPi.Display.Models;
using EnviroPi.Display.Services;
using EnviroPi.Display.Workers;
using EnviroPi.Shared.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection("MqttOptions"));
builder.Services.Configure<DisplayOptions>(
    builder.Configuration.GetSection("Display"));

// Register the display as a Singleton (one I2C connection shared by everything that renders)
builder.Services.AddSingleton<IDisplayService, OledDisplayService>();

// Register MQTT Receiver Worker
builder.Services.AddHostedService<MqttReceiverWorker>();

// Push button that switches between the temperature/humidity and CO2 pages
builder.Services.AddHostedService<ButtonWorker>();

builder.Services.AddSystemd();

var host = builder.Build();
host.Run();
