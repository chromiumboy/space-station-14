using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

public sealed class MonotoneCheckBox : CheckBox
{
    public const string StyleClassCheckBoxWhite = "checkBoxWhite";
    public const string StyleClassCheckBoxWhiteChecked = "checkBoxWhiteChecked";

    public MonotoneCheckBox()
    {
        Label.AddStyleClass("ConsoleText");

        TextureRect.RemoveStyleClass(StyleClassCheckBox);
        TextureRect.AddStyleClass(StyleClassCheckBoxWhite);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (TextureRect != null)
        {
            if (Pressed)
                TextureRect.AddStyleClass(StyleClassCheckBoxWhiteChecked);
            else
                TextureRect.RemoveStyleClass(StyleClassCheckBoxWhiteChecked);
        }
    }
}
