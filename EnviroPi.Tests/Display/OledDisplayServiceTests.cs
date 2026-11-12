using EnviroPi.Display.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Display;

public class OledDisplayServiceTests
{
    [Fact]
    public void Degrades_gracefully_when_no_display_hardware_is_present()
    {
        // On a dev machine / CI there is no I2C bus; construction and rendering must not throw.
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance);

        var ex = Record.Exception(() => display.RenderTelemetry(21.5, 48.3));

        Assert.Null(ex);
    }
}
