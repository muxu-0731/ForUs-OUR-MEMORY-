using Godot;

public partial class Player
{
    public override void _EnterTree()
    {
        if (GetNodeOrNull<EnergyBar>("EnergyBar") != null)
        {
            return;
        }

        AddChild(new EnergyBar
        {
            Name = "EnergyBar"
        });
    }
}
