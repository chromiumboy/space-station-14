using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.Label;

namespace Content.Client.UserInterface.Controls;

public sealed class MonotoneButton : ContainerButton
{
    /// <summary>
    /// Specifies the color of the button's background element
    /// </summary>
    public Color BackgroundColor { set; get; } = new Color(0.2f, 0.2f, 0.2f);

    public Label Label { get; }

    private readonly IResourceCache _resourceCache;

    public MonotoneButton()
    {
        IoCManager.InjectDependencies(this);

        _resourceCache = IoCManager.Resolve<IResourceCache>();

        Label = new Label
        {
            StyleClasses = { StyleClassButton }
        };
        AddChild(Label);

        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (_resourceCache == null)
            return;

        // Recolor label
        if (Label != null)
            Label.ModulateSelfOverride = DrawMode == DrawModeEnum.Pressed ? BackgroundColor : null;

        // Appearance modulations
        Modulate = Disabled ? Color.Gray : Color.White;
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateAppearance();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateAppearance();
    }

    /// <summary>
    ///     How to align the text inside the button.
    /// </summary>
    [ViewVariables]
    public AlignMode TextAlign { get => Label.Align; set => Label.Align = value; }

    /// <summary>
    ///     If true, the button will allow shrinking and clip text
    ///     to prevent the text from going outside the bounds of the button.
    ///     If false, the minimum size will always fit the contained text.
    /// </summary>
    [ViewVariables]
    public bool ClipText { get => Label.ClipText; set => Label.ClipText = value; }

    /// <summary>
    ///     The text displayed by the button.
    /// </summary>
    [ViewVariables]
    public string? Text { get => Label.Text; set => Label.Text = value; }
}
