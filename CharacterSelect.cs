using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CharacterSelect : Control
{
    private class CharacterInfo
    {
        public string Name;
        public string TexturePath;
    }

    private partial class CharacterCard : Button
    {
        public CharacterInfo Info;
        public TextureRect Portrait;
        public Label NameLabel;
        public Label OrderLabel;
    }

    private readonly List<CharacterInfo> _characters = new()
    {
        new CharacterInfo { Name = "外卖猫", TexturePath = "res://character_picture/deliverycat.png" },
        new CharacterInfo { Name = "邪恶兔", TexturePath = "res://character_picture/evilrabbit.png" },
        new CharacterInfo { Name = "小鸡", TexturePath = "res://character_picture/chicken.png" },
        new CharacterInfo { Name = "苹果大王", TexturePath = "res://character_picture/apple_queen.png" }
    };

    private readonly List<CharacterInfo> _selectedTeam = new();
    private readonly Dictionary<string, CharacterCard> _cardsByName = new();

    private GridContainer _grid;
    private VBoxContainer _teamList;
    private Label _messageLabel;
    private Button _clearButton;
    private Button _backButton;
    private Button _confirmButton;
    public override void _Ready()
    {
        BuildUi();
        SyncButtonsState();
    }

    private void BuildUi()
    {
        var bg = new TextureRect
        {
            Name = "BgImage",
            AnchorsPreset = (int)LayoutPreset.FullRect,
            OffsetRight = 0,
            OffsetBottom = 0,
            Texture = GD.Load<Texture2D>("res://sucaibao/GameAssets/background.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };
        AddChild(bg);

        var root = new MarginContainer
        {
            Name = "Root",
            AnchorsPreset = (int)LayoutPreset.FullRect,
        };
        root.AddThemeConstantOverride("margin_left", 24);
        root.AddThemeConstantOverride("margin_top", 24);
        root.AddThemeConstantOverride("margin_right", 24);
        root.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(root);

        var row = new HBoxContainer { Name = "Row" };
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 24);
        root.AddChild(row);

        var left = new VBoxContainer { Name = "LeftPanel" };
        left.CustomMinimumSize = new Vector2(0, 0);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        left.AddThemeConstantOverride("separation", 16);
        row.AddChild(left);

        var title = new Label
        {
            Text = "角色选择",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 58);
        left.AddChild(title);

        _grid = new GridContainer
        {
            Name = "CharacterGrid",
            Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _grid.AddThemeConstantOverride("h_separation", 16);
        _grid.AddThemeConstantOverride("v_separation", 16);
        left.AddChild(_grid);

        foreach (var character in _characters)
        {
            var card = CreateCharacterCard(character);
            _cardsByName[character.Name] = card;
            _grid.AddChild(card);
        }

        var right = new VBoxContainer { Name = "RightPanel" };
        right.CustomMinimumSize = new Vector2(320, 0);
        right.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        right.AddThemeConstantOverride("separation", 12);
        row.AddChild(right);

        var teamTitle = new Label
        {
            Text = "已选队伍",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        teamTitle.AddThemeFontSizeOverride("font_size", 35);
        right.AddChild(teamTitle);

        _teamList = new VBoxContainer { Name = "TeamList" };
        _teamList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _teamList.AddThemeConstantOverride("separation", 6);
        right.AddChild(_teamList);

        _messageLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 28);
        right.AddChild(_messageLabel);

        _confirmButton = CreateMenuButton("确认选择");
        _confirmButton.Pressed += OnConfirmPressed;
        right.AddChild(_confirmButton);

        _clearButton = CreateMenuButton("清空队伍");
        _clearButton.Pressed += OnClearPressed;
        right.AddChild(_clearButton);

        _backButton = CreateMenuButton("返回主菜单");
        _backButton.Pressed += OnBackPressed;
        right.AddChild(_backButton);
    }

    private CharacterCard CreateCharacterCard(CharacterInfo character)
    {
        var card = new CharacterCard
        {
            Info = character,
            Text = "",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(220, 220)
        };
        ApplyMenuButtonStyle(card);

        var box = new VBoxContainer { Name = "CardBox" };
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.Alignment = BoxContainer.AlignmentMode.Center;
        card.AddChild(box);

        var portrait = new TextureRect
        {
            Name = "Portrait",
            CustomMinimumSize = new Vector2(128, 128),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        portrait.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        portrait.Texture = GD.Load<Texture2D>(character.TexturePath);
        box.AddChild(portrait);

        var nameLabel = new Label
        {
            Name = "Name",
            Text = character.Name,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 30);
        box.AddChild(nameLabel);

        var orderLabel = new Label
        {
            Name = "Order",
            Text = ""
        };
        orderLabel.AddThemeFontSizeOverride("font_size", 22);
        orderLabel.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(orderLabel);

        card.Pressed += () => ToggleCharacter(character.Name);
        card.Portrait = portrait;
        card.NameLabel = nameLabel;
        card.OrderLabel = orderLabel;
        card.Toggled += pressed => UpdateCardState(card, pressed);
        return card;
    }

    private Button CreateMenuButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(218, 81),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        ApplyMenuButtonStyle(button);
        return button;
    }

    private void ApplyMenuButtonStyle(Control control)
    {
        control.AddThemeFontSizeOverride("font_size", 35);
        control.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
        control.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.95f, 0.95f, 1f));
        control.AddThemeColorOverride("font_pressed_color", new Color(1f, 1f, 1f, 1f));
        control.AddThemeColorOverride("font_focus_color", new Color(1f, 1f, 1f, 1f));
        control.AddThemeStyleboxOverride("normal", MakeStyle(new Color(0.255f, 0.192f, 0.129f, 1f), 2));
        control.AddThemeStyleboxOverride("hover", MakeStyle(new Color(0.325f, 0.251f, 0.165f, 1f), 2));
        control.AddThemeStyleboxOverride("pressed", MakeStyle(new Color(0.173f, 0.129f, 0.086f, 1f), 2));
        control.AddThemeStyleboxOverride("focus", GD.Load<StyleBox>("res://buttonfocus.tres"));
        if (control is Button button)
        {
            button.Flat = false;
        }
    }

    private StyleBoxFlat MakeStyle(Color color, int border = 0)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            BorderWidthLeft = border,
            BorderWidthTop = border,
            BorderWidthRight = border,
            BorderWidthBottom = border,
            BorderColor = new Color(0.9663301f, 0.942024f, 0.9916989f, 1f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2
        };
    }

    private void ToggleCharacter(string characterName)
    {
        var card = _cardsByName[characterName];
        if (_selectedTeam.Any(x => x.Name == characterName))
        {
            _selectedTeam.RemoveAll(x => x.Name == characterName);
            card.ButtonPressed = false;
            UpdateTeamUi();
            return;
        }

        if (_selectedTeam.Count >= 3)
        {
            ShowMessage("最多只能选择3个角色");
            return;
        }

        var info = _characters.First(x => x.Name == characterName);
        _selectedTeam.Add(info);
        card.ButtonPressed = true;
        UpdateTeamUi();
    }

    private void UpdateCardState(CharacterCard card, bool pressed)
    {
        var selectedIndex = _selectedTeam.FindIndex(x => x.Name == card.Info.Name);
        card.OrderLabel.Text = selectedIndex >= 0 ? $"{selectedIndex + 1}. 已选择" : "";
        card.Modulate = pressed ? new Color(1f, 1f, 1f, 0.88f) : Colors.White;
    }

    private void UpdateTeamUi()
    {
        foreach (var card in _cardsByName.Values)
        {
            var index = _selectedTeam.FindIndex(x => x.Name == card.Info.Name);
            card.ButtonPressed = index >= 0;
            card.OrderLabel.Text = index >= 0 ? $"{index + 1}. 已选择" : "";
        }

        foreach (var child in _teamList.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < _selectedTeam.Count; i++)
        {
            var label = new Label
            {
                Text = $"{i + 1}. {_selectedTeam[i].Name}"
            };
            label.AddThemeFontSizeOverride("font_size", 28);
            _teamList.AddChild(label);
        }

        SyncButtonsState();
    }

    private void SyncButtonsState()
    {
        _confirmButton.Disabled = _selectedTeam.Count != 3;
    }

    private void OnClearPressed()
    {
        _selectedTeam.Clear();
        foreach (var card in _cardsByName.Values)
        {
            card.ButtonPressed = false;
            card.OrderLabel.Text = "";
        }
        UpdateTeamUi();
        ShowMessage("已清空队伍");
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://main_menu.tscn");
    }

    private void OnConfirmPressed()
    {
        if (_selectedTeam.Count != 3)
        {
            ShowMessage("请先选满3个角色");
            return;
        }

        var global = GlobalScript.Instance;
        if (global == null) return;

        global.SelectedTeam.Clear();
        foreach (var character in _selectedTeam)
        {
            global.SelectedTeam.Add(new GlobalScript.SelectedCharacterData(character.Name, character.TexturePath));
        }

        GetTree().ChangeSceneToFile("res://main.tscn");
    }

    private void ShowMessage(string message)
    {
        if (_messageLabel != null)
        {
            _messageLabel.Text = message;
        }
    }
}
