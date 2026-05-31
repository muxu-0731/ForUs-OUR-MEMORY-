using Godot;

public partial class Ui
{
    public override void _EnterTree()
    {
        if (GetNodeOrNull<FocusBar>("FocusBar") != null)
        {
            return;
        }

        AddChild(new FocusBar
        {
            Name = "FocusBar"
        });
    }
}
