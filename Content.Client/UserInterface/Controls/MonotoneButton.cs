using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

public sealed class MonotoneButton : Button
{
    /// <summary>
    /// Specifies the color of the button's background element
    /// </summary>
    public Color BackgroundColor { set; get; } = new Color(0.2f, 0.2f, 0.2f);

    /// <summary>
    /// Describes the general shape of the button (i.e., open vs closed).
    /// </summary>
    public MonotoneButtonShape Shape
    {
        get { return _shape; }
        set { _shape = value; UpdateAppearance(); }
    }

    private MonotoneButtonShape _shape = MonotoneButtonShape.Closed;

    // Hollow buttons
    // Since the texture isn't uniform, we can'no't use AtlasTexture to select
    // a subregion of the texture as is done for other buttons. 
    private string[] _buttons =
        ["/Textures/Interface/Nano/Monotone/monotone_button.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_left.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_right.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_both.svg.96dpi.png"];

    // Filled buttons
    // This will be treat these the same as the hollow buttons to ensure consistency
    private string[] _buttonsFilled =
        ["/Textures/Interface/Nano/Monotone/monotone_button_filled.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_left_filled.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_right_filled.svg.96dpi.png",
        "/Textures/Interface/Nano/Monotone/monotone_button_open_both_filled.svg.96dpi.png"];

    private readonly IResourceCache _resourceCache;

    public MonotoneButton()
    {
        IoCManager.InjectDependencies(this);

        _resourceCache = IoCManager.Resolve<IResourceCache>();

        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (_resourceCache == null)
            return;

        var isPressed = (DrawMode == DrawModeEnum.Pressed);

        // Recolor label
        if (Label != null)
            Label.ModulateSelfOverride = isPressed ? BackgroundColor : null;

        // Get button texture
        var buttonTexture = _buttons[(int)Shape];
        var buttonFilledTexture = _buttonsFilled[(int)Shape];

        // Apply button texture
        var buttonbase = new StyleBoxTexture();
        buttonbase.SetPatchMargin(StyleBox.Margin.All, 11);
        buttonbase.SetPadding(StyleBox.Margin.All, 1);
        buttonbase.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        buttonbase.SetContentMarginOverride(StyleBox.Margin.Horizontal, 14);
        buttonbase.Texture = _resourceCache.GetTexture(isPressed ? buttonFilledTexture : buttonTexture);

        // We don't want generic button styles being applied, only this one
        this.StyleBoxOverride = buttonbase;
    }

    protected override void DrawModeChanged()
    {
        UpdateAppearance();
    }
}

public enum MonotoneButtonShape : byte
{
    Closed = 0,
    OpenLeft = 1,
    OpenRight = 2,
    OpenBoth = 3
}
