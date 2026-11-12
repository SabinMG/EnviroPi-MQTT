namespace EnviroPi.Display.Models;

/// <summary>Settings bound from the "Display" section of appsettings.json.</summary>
public class DisplayOptions
{
    /// <summary>
    /// BCM GPIO number of the page button, wired between that pin and GND
    /// (the internal pull-up is used). <c>null</c> disables the button.
    /// Default: GPIO17, header pin 11.
    /// </summary>
    public int? ButtonGpioPin { get; set; } = 17;
}
