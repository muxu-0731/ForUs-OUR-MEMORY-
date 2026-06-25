using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class CharacterSelect : Control
{
    private const int CharactersPerPage = 4;
    private const int MaxPartySize = GlobalScript.MaxPartySize;

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

    private readonly List<CharacterInfo> _characters = new();
    private static readonly JsonSerializerOptions CharacterJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<CharacterInfo> _selectedTeam = new();
    private readonly Dictionary<string, CharacterCard> _cardsByName = new();

    private GridContainer _grid;
    private VBoxContainer _teamList;
    private Label _messageLabel;
    private Button _clearButton;
    private Button _backButton;
    private Button _confirmButton;
    private Button _prevPageButton;
    private Button _nextPageButton;
    private int _currentPage = 0;

    public override void _Ready()
    {
        LoadCharactersFromJson();
        BuildUi();
        RefreshPage();
        SyncButtonsState();
    }

    private void LoadCharactersFromJson()
    {
        _characters.Clear();

        using var dir = DirAccess.Open("res://character_data");
        if (dir == null)
        {
            GD.PrintErr($"角色选择加载失败：无法打开角色数据目录，错误码：{DirAccess.GetOpenError()}");
            return;
        }

        foreach (string fileName in dir.GetFiles().OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string jsonPath = $"res://character_data/{fileName}";
            try
            {
                using var file = Godot.FileAccess.Open(jsonPath, Godot.FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"角色选择加载失败：无法打开 {jsonPath}，错误码：{Godot.FileAccess.GetOpenError()}");
                    continue;
                }

                var config = JsonSerializer.Deserialize<CharacterConfig>(file.GetAsText(), CharacterJsonOptions);
                if (config == null || !config.IsPlayerUnit || string.IsNullOrWhiteSpace(config.UnitName) || string.IsNullOrWhiteSpace(config.TexturePath))
                {
                    continue;
                }

                if (_characters.Any(character => character.Name == config.UnitName))
                {
                    GD.PrintErr($"角色选择加载跳过重复角色：{config.UnitName}");
                    continue;
                }

                _characters.Add(new CharacterInfo
                {
                    Name = config.UnitName,
                    TexturePath = config.TexturePath
                });
            }
            catch (Exception ex)
            {
                GD.PrintErr($"角色选择解析失败：{jsonPath}，异常：{ex.Message}");
            }
        }
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

        var pageControls = new HBoxContainer { Name = "PageControls" };
        pageControls.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        pageControls.Alignment = BoxContainer.AlignmentMode.Center;
        pageControls.AddThemeConstantOverride("separation", 16);
        left.AddChild(pageControls);

        _prevPageButton = CreateMenuButton("上一页");
        _prevPageButton.Pressed += OnPrevPagePressed;
        pageControls.AddChild(_prevPageButton);

        _nextPageButton = CreateMenuButton("下一页");
        _nextPageButton.Pressed += OnNextPagePressed;
        pageControls.AddChild(_nextPageButton);

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

        if (_selectedTeam.Count >= MaxPartySize)
        {
            ShowMessage($"最多只能选择{MaxPartySize}个角色");
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
        _confirmButton.Disabled = _selectedTeam.Count != MaxPartySize;
        RefreshPageButtons();
    }

    private int GetTotalPages()
    {
        return Mathf.Max(1, Mathf.CeilToInt((float)_characters.Count / CharactersPerPage));
    }

    private void RefreshPage()
    {
        int totalPages = GetTotalPages();
        _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);

        int startIndex = _currentPage * CharactersPerPage;
        int endIndex = Mathf.Min(startIndex + CharactersPerPage, _characters.Count);

        for (int i = 0; i < _characters.Count; i++)
        {
            var character = _characters[i];
            if (_cardsByName.TryGetValue(character.Name, out var card))
            {
                card.Visible = i >= startIndex && i < endIndex;
            }
        }

        RefreshPageButtons();
    }

    private void RefreshPageButtons()
    {
        int totalPages = GetTotalPages();
        bool hasMultiplePages = totalPages > 1;

        if (_prevPageButton != null)
        {
            _prevPageButton.Visible = hasMultiplePages;
            _prevPageButton.Disabled = !hasMultiplePages || _currentPage <= 0;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.Visible = hasMultiplePages;
            _nextPageButton.Disabled = !hasMultiplePages || _currentPage >= totalPages - 1;
        }
    }

    private void OnPrevPagePressed()
    {
        if (_currentPage <= 0)
        {
            return;
        }

        _currentPage--;
        RefreshPage();
    }

    private void OnNextPagePressed()
    {
        int totalPages = GetTotalPages();
        if (_currentPage >= totalPages - 1)
        {
            return;
        }

        _currentPage++;
        RefreshPage();
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
        if (_selectedTeam.Count != MaxPartySize)
        {
            ShowMessage($"请先选满{MaxPartySize}个角色");
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
