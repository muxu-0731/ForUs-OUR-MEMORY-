using Godot;
using System;

public partial class Main : Node2D
{
    public override void _EnterTree()
    {
        if (GlobalScript.Instance != null)
        {
            GlobalScript.Instance.ResetBattleFromSelectedTeam();
        }
    }

	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
	}
}
