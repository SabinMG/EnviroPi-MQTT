namespace EnviroPi.Display.Rendering;

/// <summary>The screens the OLED can show; the push button cycles through them.</summary>
public enum DisplayPage
{
    /// <summary>Temperature and humidity.</summary>
    Climate,

    /// <summary>CO₂ concentration with a ventilation hint.</summary>
    Co2
}
