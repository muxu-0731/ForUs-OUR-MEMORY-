using Godot;

public partial class Enemy
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
