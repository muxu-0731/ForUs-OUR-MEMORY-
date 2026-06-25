using Godot;

public partial class MainMenu : Node2D
{
    private Label _titleLabel;
    private Button _chooseCharacterButton;
    private Button _quitButton;

    public override void _Ready()
    {
        _titleLabel = GetNodeOrNull<Label>("CanvasLayer/TitleLabel");
        _chooseCharacterButton = GetNodeOrNull<Button>("CanvasLayer/ChooseCharacter");
        _quitButton = GetNodeOrNull<Button>("CanvasLayer/BtnQuit");

        if (_titleLabel == null)
        {
            GD.PrintErr("MainMenu: missing CanvasLayer/TitleLabel node.");
        }

        if (_chooseCharacterButton == null)
        {
            GD.PrintErr("MainMenu: missing CanvasLayer/ChooseCharacter node.");
        }

        if (_quitButton == null)
        {
            GD.PrintErr("MainMenu: missing CanvasLayer/BtnQuit node.");
        }

        // UI layout is now authored in the scene under CanvasLayer, so no Control-based positioning is applied here.
        if (_titleLabel != null)
        {
            _titleLabel.Visible = true;
        }

        if (_chooseCharacterButton != null)
        {
            _chooseCharacterButton.Visible = true;
        }

        if (_quitButton != null)
        {
            _quitButton.Visible = true;
        }
    }

    private void OnChooseCharacterPressed()
    {
        GetTree().ChangeSceneToFile("res://character_select.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
