using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class CharacterSelectSceneController : Control
{
    private const int CharactersPerPage = 6;
    private const string DetailScenePath = "res://Scenes/CharacterDetailScene.tscn";
    private const string MainMenuScenePath = "res://main_menu.tscn";
    private const string BattleScenePath = "res://main.tscn";

    private readonly List<CharacterCatalogEntry> _characters = new();
    private readonly List<CharacterCardBinding> _cards = new();
    private readonly List<TeamSlotBinding> _teamSlots = new();

    private Control _leftVBox;
    private ScrollContainer _characterScroll;
    private GridContainer _characterGrid;
    private Control _cardTemplate;
    private Control _pageBar;
    private Button _prevPageButton;
    private Label _pageLabel;
    private Button _nextPageButton;
    private Control _teamSlotsRoot;
    private Button _confirmButton;
    private Button _clearButton;
    private Button _backButton;
    private int _currentPage;

    private sealed class CharacterCardBinding
    {
        public CharacterCatalogEntry Character;
        public Control Root;
        public TextureRect Portrait;
        public Label NameLabel;
        public Label MajorLabel;
        public Label StatHintLabel;
        public Button SelectButton;
        public Button DetailButton;
        public string DefaultSelectButtonText = string.Empty;
    }

    private sealed class TeamSlotBinding
    {
        public int Index;
        public Label NameLabel;
        public Label MajorLabel;
        public string DefaultNameText = string.Empty;
        public string DefaultMajorText = string.Empty;
    }

    public override void _Ready()
    {
        BindNodes();
        EnsureLayout();
        ConnectSignals();
        LoadCharacters();
        BuildTeamSlots();
        BuildCards();

        _currentPage = Mathf.Clamp(CharacterSelectionContext.CurrentSelectPage, 0, GetTotalPages() - 1);
        RefreshAll();
        CallDeferred(nameof(LogLayoutState));
    }

    private void BindNodes()
    {
        _leftVBox = FindNodeByName<Control>(this, "LeftVBox");
        _characterScroll = FindNodeByName<ScrollContainer>(this, "CharacterScroll");
        _characterGrid = FindNodeByName<GridContainer>(this, "CharacterGrid");
        _cardTemplate = FindNodeByName<Control>(this, "CardTemplate");
        _pageBar = FindNodeByName<Control>(this, "PageBar");
        _prevPageButton = FindNodeByName<Button>(this, "PrevPageButton");
        _pageLabel = FindNodeByName<Label>(this, "PageLabel");
        _nextPageButton = FindNodeByName<Button>(this, "NextPageButton");
        _teamSlotsRoot = FindNodeByName<Control>(this, "TeamSlots");
        _confirmButton = FindNodeByName<Button>(this, "ConfirmButton");
        _clearButton = FindNodeByName<Button>(this, "ClearButton");
        _backButton = FindNodeByName<Button>(this, "BackButton");

        if (_cardTemplate != null)
        {
            _cardTemplate.Visible = false;
        }
    }

    private void EnsureLayout()
    {
        if (_leftVBox != null)
        {
            _leftVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _leftVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }

        if (_characterScroll != null)
        {
            _characterScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _characterScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _characterScroll.ClipContents = true;
            _characterScroll.MouseFilter = MouseFilterEnum.Pass;
        }

        if (_characterGrid != null)
        {
            _characterGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _characterGrid.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _characterGrid.Columns = 3;
        }
    }

    private void ConnectSignals()
    {
        if (_prevPageButton != null)
        {
            _prevPageButton.Pressed += OnPrevPagePressed;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.Pressed += OnNextPagePressed;
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        if (_clearButton != null)
        {
            _clearButton.Pressed += OnClearPressed;
        }

        if (_backButton != null)
        {
            _backButton.Pressed += OnBackPressed;
        }
    }

    private void LoadCharacters()
    {
        _characters.Clear();
        _characters.AddRange(CharacterDataRepository.LoadPlayerCharacters());
        CharacterSelectionContext.RebindSelection(_characters);
        GD.Print($"CharacterSelectSceneController: loaded {_characters.Count} player characters.");
    }

    private void BuildTeamSlots()
    {
        _teamSlots.Clear();
        if (_teamSlotsRoot == null)
        {
            return;
        }

        for (int i = 1; i <= GlobalScript.MaxPartySize; i++)
        {
            if (FindNodeByName<Control>(_teamSlotsRoot, $"Slot{i}") is not Control slotRoot)
            {
                continue;
            }

            Label nameLabel = FindNodeByName<Label>(slotRoot, "SlotNameLabel");
            Label majorLabel = FindNodeByName<Label>(slotRoot, "SlotMajorLabel");
            if (nameLabel == null || majorLabel == null)
            {
                continue;
            }

            _teamSlots.Add(new TeamSlotBinding
            {
                Index = i - 1,
                NameLabel = nameLabel,
                MajorLabel = majorLabel,
                DefaultNameText = nameLabel.Text,
                DefaultMajorText = majorLabel.Text
            });
        }
    }

    private void BuildCards()
    {
        _cards.Clear();
        if (_characterGrid == null || _cardTemplate == null)
        {
            GD.PrintErr("CharacterSelectSceneController: missing CharacterGrid or CardTemplate.");
            return;
        }

        foreach (Node child in _characterGrid.GetChildren())
        {
            if (child != _cardTemplate)
            {
                child.QueueFree();
            }
        }

        foreach (CharacterCatalogEntry character in _characters)
        {
            if (_cardTemplate.Duplicate() is not Control cardRoot)
            {
                continue;
            }

            cardRoot.Name = $"Card_{SanitizeName(character.UnitName)}";
            cardRoot.Visible = true;
            cardRoot.CustomMinimumSize = _cardTemplate.CustomMinimumSize == Vector2.Zero
                ? new Vector2(180f, 240f)
                : _cardTemplate.CustomMinimumSize;
            cardRoot.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            cardRoot.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            cardRoot.Modulate = Colors.White;
            cardRoot.SelfModulate = Colors.White;
            cardRoot.ZIndex = 1;

            CanvasItem cardVBox = FindNodeByName<CanvasItem>(cardRoot, "CardVBox");
            if (cardVBox != null)
            {
                cardVBox.Visible = true;
            }

            var binding = new CharacterCardBinding
            {
                Character = character,
                Root = cardRoot,
                Portrait = FindNodeByName<TextureRect>(cardRoot, "Portrait"),
                NameLabel = FindNodeByNames<Label>(cardRoot, "NameLabel", "NameLab"),
                MajorLabel = FindNodeByNames<Label>(cardRoot, "MajorLabel", "MajorLab"),
                StatHintLabel = FindNodeByNames<Label>(cardRoot, "StatHintLabel", "StatHint"),
                SelectButton = FindNodeByNames<Button>(cardRoot, "SelectButton", "SelectB"),
                DetailButton = FindNodeByNames<Button>(cardRoot, "DetailButton", "DetailB")
            };

            if (binding.SelectButton != null)
            {
                binding.DefaultSelectButtonText = binding.SelectButton.Text;
                binding.SelectButton.Pressed += () => OnCharacterSelectPressed(binding.Character);
            }

            if (binding.DetailButton != null)
            {
                binding.DetailButton.Pressed += () => OnCharacterDetailPressed(binding.Character);
            }

            ApplyCharacterCardContent(binding);
            _cards.Add(binding);
            _characterGrid.AddChild(cardRoot);
            cardRoot.SetDeferred("visible", true);
            if (cardVBox != null)
            {
                cardVBox.SetDeferred("visible", true);
            }

            GD.Print($"Card: {cardRoot.Name}, path={cardRoot.GetPath()}, parent={cardRoot.GetParent()?.Name}, visible={cardRoot.Visible}, pos={cardRoot.GlobalPosition}, size={cardRoot.Size}, min={cardRoot.CustomMinimumSize}");
        }

        _characterGrid.QueueSort();
        GD.Print($"CharacterSelectSceneController: created {_cards.Count} character cards.");
    }

    private void ApplyCharacterCardContent(CharacterCardBinding card)
    {
        if (card.Portrait != null)
        {
            ApplyCharacterCardPortrait(card.Portrait, card.Character);
        }

        if (card.NameLabel != null)
        {
            card.NameLabel.Text = card.Character.UnitName;
        }

        if (card.MajorLabel != null)
        {
            card.MajorLabel.Text = $"主修：{GetMajorText(card.Character)}";
        }

        if (card.StatHintLabel != null)
        {
            card.StatHintLabel.Text = $"生命 {FormatNumber(card.Character.HpMax)} / 攻击 {FormatNumber(card.Character.Attack)} / 能量 {card.Character.MaxEnergy}";
        }
    }

    private static void ApplyCharacterCardPortrait(TextureRect portraitRect, CharacterCatalogEntry character)
    {
        if (portraitRect == null)
        {
            return;
        }

        portraitRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;

        Texture2D portraitTexture = CharacterDataRepository.LoadTextureSafe(character?.PortraitPath);
        if (portraitTexture != null)
        {
            if (TryBuildSelectionPortraitTexture(character, portraitTexture, out Texture2D cardTexture, out bool usedCrop))
            {
                portraitRect.Texture = cardTexture;
                portraitRect.StretchMode = usedCrop
                    ? TextureRect.StretchModeEnum.KeepAspectCovered
                    : TextureRect.StretchModeEnum.KeepAspectCentered;
                return;
            }
        }
        else if (!string.IsNullOrWhiteSpace(character?.PortraitPath))
        {
            GD.PushWarning($"CharacterSelectSceneController: failed to load PortraitPath for '{character.UnitName}': {character.PortraitPath}");
        }

        portraitRect.Texture = CharacterDataRepository.LoadTextureSafe(character?.TexturePath);
        portraitRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }

    private static bool TryBuildSelectionPortraitTexture(
        CharacterCatalogEntry character,
        Texture2D portraitTexture,
        out Texture2D cardTexture,
        out bool usedCrop)
    {
        cardTexture = portraitTexture;
        usedCrop = false;

        PortraitCropConfig crop = character?.SelectionPortraitCrop;
        if (crop == null)
        {
            return true;
        }

        Vector2 textureSize = portraitTexture.GetSize();
        Rect2? region = BuildSafeCropRegion(character, crop, textureSize);
        if (region == null)
        {
            return true;
        }

        var atlas = new AtlasTexture
        {
            Atlas = portraitTexture,
            Region = region.Value
        };
        cardTexture = atlas;
        usedCrop = true;
        return true;
    }

    private static Rect2? BuildSafeCropRegion(CharacterCatalogEntry character, PortraitCropConfig crop, Vector2 textureSize)
    {
        if (crop == null)
        {
            return null;
        }

        if (!IsFiniteCropValue(crop.X) ||
            !IsFiniteCropValue(crop.Y) ||
            !IsFiniteCropValue(crop.Width) ||
            !IsFiniteCropValue(crop.Height))
        {
            GD.PushWarning($"CharacterSelectSceneController: invalid portrait crop values for '{character?.UnitName}'. Falling back to full portrait.");
            return null;
        }

        if (crop.Width <= 0f || crop.Height <= 0f)
        {
            GD.PushWarning($"CharacterSelectSceneController: portrait crop size must be positive for '{character?.UnitName}'. Falling back to full portrait.");
            return null;
        }

        float clampedX = Mathf.Clamp(crop.X, 0f, textureSize.X);
        float clampedY = Mathf.Clamp(crop.Y, 0f, textureSize.Y);
        float maxWidth = Mathf.Max(0f, textureSize.X - clampedX);
        float maxHeight = Mathf.Max(0f, textureSize.Y - clampedY);
        float clampedWidth = Mathf.Min(crop.Width, maxWidth);
        float clampedHeight = Mathf.Min(crop.Height, maxHeight);

        if (clampedWidth <= 0f || clampedHeight <= 0f)
        {
            GD.PushWarning($"CharacterSelectSceneController: portrait crop region is outside the texture bounds for '{character?.UnitName}'. Falling back to full portrait.");
            return null;
        }

        if (!Mathf.IsEqualApprox(clampedX, crop.X) ||
            !Mathf.IsEqualApprox(clampedY, crop.Y) ||
            !Mathf.IsEqualApprox(clampedWidth, crop.Width) ||
            !Mathf.IsEqualApprox(clampedHeight, crop.Height))
        {
            GD.PushWarning($"CharacterSelectSceneController: portrait crop for '{character?.UnitName}' exceeded texture bounds and was clamped.");
        }

        return new Rect2(clampedX, clampedY, clampedWidth, clampedHeight);
    }

    private static bool IsFiniteCropValue(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void RefreshAll()
    {
        RefreshCards();
        RefreshTeamSlots();
        RefreshPagination();
        RefreshButtons();
    }

    private void RefreshCards()
    {
        int startIndex = _currentPage * CharactersPerPage;
        int endIndex = Mathf.Min(startIndex + CharactersPerPage, _cards.Count);
        bool teamIsFull = CharacterSelectionContext.SelectedTeam.Count >= GlobalScript.MaxPartySize;

        for (int i = 0; i < _cards.Count; i++)
        {
            CharacterCardBinding card = _cards[i];
            bool isSelected = CharacterSelectionContext.IsSelected(card.Character);

            card.Root.Visible = i >= startIndex && i < endIndex;
            card.Root.SelfModulate = isSelected ? new Color(0.84f, 1.0f, 1.0f, 1.0f) : Colors.White;

            if (card.SelectButton != null)
            {
                string defaultText = string.IsNullOrWhiteSpace(card.DefaultSelectButtonText) ? "选择" : card.DefaultSelectButtonText;
                card.SelectButton.Text = isSelected ? "已选" : defaultText;
                card.SelectButton.Disabled = !isSelected && teamIsFull;
            }
        }
    }

    private void RefreshTeamSlots()
    {
        for (int i = 0; i < _teamSlots.Count; i++)
        {
            TeamSlotBinding slot = _teamSlots[i];
            if (i < CharacterSelectionContext.SelectedTeam.Count)
            {
                CharacterCatalogEntry character = CharacterSelectionContext.SelectedTeam[i];
                slot.NameLabel.Text = $"{i + 1}. {character.UnitName}";
                slot.MajorLabel.Text = $"主修：{GetMajorText(character)}";
            }
            else
            {
                slot.NameLabel.Text = string.IsNullOrWhiteSpace(slot.DefaultNameText) ? $"{i + 1}. 空位" : slot.DefaultNameText;
                slot.MajorLabel.Text = string.IsNullOrWhiteSpace(slot.DefaultMajorText) ? "等待同调" : slot.DefaultMajorText;
            }
        }
    }

    private void RefreshPagination()
    {
        int totalPages = GetTotalPages();
        CharacterSelectionContext.CurrentSelectPage = _currentPage;

        if (_pageLabel != null)
        {
            _pageLabel.Text = $"{_currentPage + 1} / {totalPages}";
        }

        if (_pageBar != null)
        {
            _pageBar.Visible = totalPages > 1;
        }

        if (_prevPageButton != null)
        {
            _prevPageButton.Disabled = _currentPage <= 0;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.Disabled = _currentPage >= totalPages - 1;
        }
    }

    private void RefreshButtons()
    {
        if (_confirmButton != null)
        {
            _confirmButton.Disabled = CharacterSelectionContext.SelectedTeam.Count != GlobalScript.MaxPartySize;
        }

        if (_clearButton != null)
        {
            _clearButton.Disabled = CharacterSelectionContext.SelectedTeam.Count == 0;
        }
    }

    private void OnCharacterSelectPressed(CharacterCatalogEntry character)
    {
        if (!CharacterSelectionContext.TryToggleCharacter(character, GlobalScript.MaxPartySize, out _))
        {
            GD.Print("CharacterSelectScene: team is already full.");
            return;
        }

        RefreshAll();
    }

    private void OnCharacterDetailPressed(CharacterCatalogEntry character)
    {
        CharacterSelectionContext.CurrentDetailCharacter = character;
        CharacterSelectionContext.CurrentSelectPage = _currentPage;
        GetTree().ChangeSceneToFile(DetailScenePath);
    }

    private void OnPrevPagePressed()
    {
        if (_currentPage <= 0)
        {
            return;
        }

        _currentPage--;
        RefreshAll();
    }

    private void OnNextPagePressed()
    {
        int totalPages = GetTotalPages();
        if (_currentPage >= totalPages - 1)
        {
            return;
        }

        _currentPage++;
        RefreshAll();
    }

    private void OnClearPressed()
    {
        CharacterSelectionContext.ClearSelection();
        RefreshAll();
    }

    private void OnConfirmPressed()
    {
        if (CharacterSelectionContext.SelectedTeam.Count != GlobalScript.MaxPartySize)
        {
            GD.Print($"CharacterSelectScene: please select {GlobalScript.MaxPartySize} characters before confirming.");
            return;
        }

        if (GlobalScript.Instance == null)
        {
            foreach (CharacterCatalogEntry character in CharacterSelectionContext.SelectedTeam)
            {
                GD.Print(character.JsonPath);
            }

            return;
        }

        GlobalScript.Instance.SelectedTeam.Clear();
        foreach (CharacterCatalogEntry character in CharacterSelectionContext.SelectedTeam)
        {
            GlobalScript.Instance.SelectedTeam.Add(new GlobalScript.SelectedCharacterData(character.UnitName, character.TexturePath));
        }

        CharacterSelectionContext.CurrentDetailCharacter = null;
        GetTree().ChangeSceneToFile(BattleScenePath);
    }

    private void OnBackPressed()
    {
        CharacterSelectionContext.CurrentDetailCharacter = null;
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private int GetTotalPages()
    {
        return Mathf.Max(1, Mathf.CeilToInt(_characters.Count / (float)CharactersPerPage));
    }

    private static string GetMajorText(CharacterCatalogEntry character)
    {
        return string.IsNullOrWhiteSpace(character?.Major) ? "--" : character.Major;
    }

    private static string FormatNumber(float value)
    {
        return Mathf.IsEqualApprox(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Character";
        }

        return value.Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace(" ", "_");
    }

    private void LogLayoutState()
    {
        if (_leftVBox != null)
        {
            GD.Print($"Layout LeftVBox: visible={_leftVBox.Visible}, size={_leftVBox.Size}, min={_leftVBox.CustomMinimumSize}, pos={_leftVBox.GlobalPosition}");
        }

        if (_characterScroll != null)
        {
            GD.Print($"Layout CharacterScroll: visible={_characterScroll.Visible}, size={_characterScroll.Size}, min={_characterScroll.CustomMinimumSize}, pos={_characterScroll.GlobalPosition}, clip={_characterScroll.ClipContents}");
        }

        if (_characterGrid != null)
        {
            GD.Print($"Layout CharacterGrid: visible={_characterGrid.Visible}, size={_characterGrid.Size}, min={_characterGrid.CustomMinimumSize}, pos={_characterGrid.GlobalPosition}, columns={_characterGrid.Columns}, childCount={_characterGrid.GetChildCount()}");
        }

        foreach (CharacterCardBinding card in _cards)
        {
            GD.Print($"Layout Card Final: {card.Root.Name}, parent={card.Root.GetParent()?.Name}, visible={card.Root.Visible}, pos={card.Root.GlobalPosition}, size={card.Root.Size}, min={card.Root.CustomMinimumSize}");
        }
    }

    private static T FindNodeByName<T>(Node root, string name) where T : class
    {
        return FindNodeByNames<T>(root, name);
    }

    private static T FindNodeByNames<T>(Node root, params string[] names) where T : class
    {
        if (root == null)
        {
            return null;
        }

        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (root.FindChild(name, true, false) is T node)
            {
                return node;
            }
        }

        return null;
    }
}
