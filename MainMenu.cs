using Godot;

public partial class MainMenu : Control
{
    private Button _chooseCharacterButton;
    private Button _quitButton;

    public override void _Ready()
    {
        _chooseCharacterButton = GetNodeOrNull<Button>("ChooseCharacter");
        _quitButton = GetNodeOrNull<Button>("BtnQuit");

        if (_chooseCharacterButton != null)
        {
            _chooseCharacterButton.Pressed += OnChooseCharacterPressed;
        }

        if (_quitButton != null)
        {
            _quitButton.Pressed += OnQuitPressed;
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
