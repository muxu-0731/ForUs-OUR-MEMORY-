using Godot;

public partial class Main : Node2D
{
    private Sprite2D _background;
    private CanvasLayer _canvasLayer;
    private Ui _battleUi;

    public override void _EnterTree()
    {
        if (GlobalScript.Instance != null)
        {
            GlobalScript.Instance.ResetBattleFromSelectedTeam();
        }
    }

    public override void _Ready()
    {
        _background = GetNodeOrNull<Sprite2D>("Background");
        _canvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
        _battleUi = GetNodeOrNull<Ui>("CanvasLayer/ui");

        if (_background == null)
        {
            GD.PrintErr("Main: missing Background Sprite2D node.");
        }
        else
        {
            // Keep the battle background pinned to the bottom-most draw layer.
            _background.ZAsRelative = false;
            _background.ZIndex = -100;
            _background.Visible = true;
        }

        if (_canvasLayer == null)
        {
            GD.PrintErr("Main: missing CanvasLayer node.");
        }
        else
        {
            // Battle UI must render in front of world sprites and the background.
            _canvasLayer.Layer = 1;
        }

        if (_battleUi == null)
        {
            GD.PrintErr("Main: missing CanvasLayer/ui node.");
        }
    }
}
