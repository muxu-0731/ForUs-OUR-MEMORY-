using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

public partial class CharacterDetailSceneController : Control
{
    private const string SelectScenePath = "res://Scenes/CharacterSelectScene.tscn";

    private static readonly NodePath FullPortraitPath = "SafeArea/MainHBox/LeftPortraitPanel/PortraitMargin/PortraitStack/FullPortrait";
    private static readonly NodePath PortraitPlaceholderPath = "SafeArea/MainHBox/LeftPortraitPanel/PortraitMargin/PortraitStack/PortraitPlaceholder";
    private static readonly NodePath PortraitNamePlatePath = "SafeArea/MainHBox/LeftPortraitPanel/PortraitMargin/PortraitStack/PortraitNamePlate";
    private static readonly NodePath NameLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/TopBar/TitleBox/NameLabel";
    private static readonly NodePath MajorLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/TopBar/TitleBox/MajorLabel";
    private static readonly NodePath BackButtonPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/TopBar/BackButton";
    private static readonly NodePath HpLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/StatPanel/StatGrid/HpLabel";
    private static readonly NodePath AttackLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/StatPanel/StatGrid/AttackLabel";
    private static readonly NodePath CritRateLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/StatPanel/StatGrid/CritRateLabel";
    private static readonly NodePath CritDamageLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/StatPanel/StatGrid/CritDamageLabel";
    private static readonly NodePath EnergyLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/StatPanel/StatGrid/EnergyLabel";
    private static readonly NodePath SkillTitleLabelPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/SkillTitleLabel";
    private static readonly NodePath SkillScrollPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/SkillScroll";
    private static readonly NodePath SkillListPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/SkillScroll/SkillList";
    private static readonly NodePath SelectButtonPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/BottomButtons/SelectButton";
    private static readonly NodePath ReturnButtonPath = "SafeArea/MainHBox/RightInfoPanel/InfoMargin/InfoVBox/BottomButtons/ReturnButton";

    private static readonly Color SkillTitleColor = new(0.9490196f, 0.827451f, 0.5411765f, 1.0f);
    private static readonly Color SkillMetaColor = new(0.49411765f, 0.8627451f, 1.0f, 1.0f);
    private static readonly Color SkillBodyColor = new(0.9647059f, 0.98039216f, 1.0f, 1.0f);

    private TextureRect _fullPortrait;
    private Label _portraitPlaceholder;
    private Label _portraitNamePlate;
    private Label _nameLabel;
    private Label _majorLabel;
    private Button _backButton;
    private Label _hpLabel;
    private Label _attackLabel;
    private Label _critRateLabel;
    private Label _critDamageLabel;
    private Label _energyLabel;
    private Label _skillTitleLabel;
    private ScrollContainer _skillScroll;
    private VBoxContainer _skillList;
    private Button _selectButton;
    private Button _returnButton;
    private string _placeholderDefaultText = string.Empty;
    private CharacterCatalogEntry _character;

    private sealed class SkillViewModel
    {
        public string DisplayName { get; set; } = string.Empty;

        public string TypeText { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<string> DetailLines { get; } = new();
    }

    public override void _Ready()
    {
        BindNodes();
        ConnectSignals();
        ConfigureSkillContainers();

        _character = CharacterSelectionContext.CurrentDetailCharacter;
        LogNodeDiagnostics();
        CallDeferred(nameof(LogDeferredLayoutDiagnostics));
        RefreshUi();
    }

    public void SetCharacter(CharacterCatalogEntry character)
    {
        _character = character;
        RefreshUi();
    }

    private void BindNodes()
    {
        _fullPortrait = GetNodeOrNull<TextureRect>(FullPortraitPath);
        _portraitPlaceholder = GetNodeOrNull<Label>(PortraitPlaceholderPath);
        _portraitNamePlate = GetNodeOrNull<Label>(PortraitNamePlatePath);
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _majorLabel = GetNodeOrNull<Label>(MajorLabelPath);
        _backButton = GetNodeOrNull<Button>(BackButtonPath);
        _hpLabel = GetNodeOrNull<Label>(HpLabelPath);
        _attackLabel = GetNodeOrNull<Label>(AttackLabelPath);
        _critRateLabel = GetNodeOrNull<Label>(CritRateLabelPath);
        _critDamageLabel = GetNodeOrNull<Label>(CritDamageLabelPath);
        _energyLabel = GetNodeOrNull<Label>(EnergyLabelPath);
        _skillTitleLabel = GetNodeOrNull<Label>(SkillTitleLabelPath);
        _skillScroll = GetNodeOrNull<ScrollContainer>(SkillScrollPath);
        _skillList = GetNodeOrNull<VBoxContainer>(SkillListPath);
        _selectButton = GetNodeOrNull<Button>(SelectButtonPath);
        _returnButton = GetNodeOrNull<Button>(ReturnButtonPath);

        if (_portraitPlaceholder != null)
        {
            _placeholderDefaultText = _portraitPlaceholder.Text;
        }

        if (_skillList == null)
        {
            GD.PushError($"CharacterDetailSceneController: SkillList node not found at {SkillListPath}");
        }
    }

    private void ConnectSignals()
    {
        if (_backButton != null)
        {
            _backButton.Pressed += GoBackToSelection;
        }

        if (_returnButton != null)
        {
            _returnButton.Pressed += GoBackToSelection;
        }

        if (_selectButton != null)
        {
            _selectButton.Pressed += OnSelectPressed;
        }
    }

    private void ConfigureSkillContainers()
    {
        if (_skillScroll != null)
        {
            _skillScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _skillScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _skillScroll.CustomMinimumSize = new Vector2(_skillScroll.CustomMinimumSize.X, 1f);
        }

        if (_skillList != null)
        {
            _skillList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _skillList.AddThemeConstantOverride("separation", 12);
        }
    }

    private void RefreshUi()
    {
        PopulateCharacter();
        RefreshSelectButton();
    }

    private void PopulateCharacter()
    {
        CharacterConfig config = _character?.Config;
        GD.Print($"Detail render {config?.UnitName ?? "<null>"}, skills={config?.Skills?.Count ?? -1}");

        ApplySkills();

        if (_character == null)
        {
            GD.PushError("CharacterDetailSceneController: missing detail character context.");
            return;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = _character.UnitName;
        }

        if (_majorLabel != null)
        {
            _majorLabel.Text = $"主修：{GetMajorText(_character)}";
        }

        if (_portraitNamePlate != null)
        {
            _portraitNamePlate.Text = _character.UnitName;
        }

        ApplyPortrait();
        ApplyStats();
    }

    private void ApplyPortrait()
    {
        Texture2D portraitTexture = CharacterDataRepository.LoadTextureSafe(_character?.PortraitPath);
        Texture2D fallbackTexture = CharacterDataRepository.LoadTextureSafe(_character?.TexturePath);
        bool hasFullPortrait = portraitTexture != null;

        if (_fullPortrait != null)
        {
            _fullPortrait.Texture = hasFullPortrait ? portraitTexture : fallbackTexture;
        }

        if (_portraitPlaceholder != null)
        {
            _portraitPlaceholder.Text = string.IsNullOrWhiteSpace(_placeholderDefaultText) ? "立绘待收录" : _placeholderDefaultText;
            _portraitPlaceholder.Visible = !hasFullPortrait;
        }
    }

    private void ApplyStats()
    {
        if (_character == null)
        {
            return;
        }

        if (_hpLabel != null)
        {
            _hpLabel.Text = $"生命：{FormatNumber(_character.HpMax)}";
        }

        if (_attackLabel != null)
        {
            _attackLabel.Text = $"攻击：{FormatNumber(_character.Attack)}";
        }

        if (_critRateLabel != null)
        {
            _critRateLabel.Text = $"暴击率：{FormatPercent(_character.CritRate)}";
        }

        if (_critDamageLabel != null)
        {
            _critDamageLabel.Text = $"暴击伤害：{FormatPercent(_character.CritDamage)}";
        }

        if (_energyLabel != null)
        {
            _energyLabel.Text = $"能量上限：{_character.MaxEnergy}";
        }
    }

    private void ApplySkills()
    {
        if (_skillTitleLabel != null)
        {
            _skillTitleLabel.Text = "技能档案";
        }

        if (_skillList == null)
        {
            GD.PushError("CharacterDetailSceneController: SkillList is null, skills cannot render.");
            return;
        }

        ClearSkillList();

        List<SkillViewModel> skillViewModels = BuildSkillViewModels(_character);
        GD.Print($"Detail render list count={skillViewModels.Count}");

        if (skillViewModels.Count == 0)
        {
            AddEmptySkillState("暂无技能档案");
            GD.Print("Detail render fallback: 暂无技能档案");
            return;
        }

        foreach (SkillViewModel skillViewModel in skillViewModels)
        {
            AddSkillEntry(skillViewModel);
        }

        GD.Print($"Detail render complete, SkillList children={_skillList.GetChildCount()}");
    }

    private void ClearSkillList()
    {
        if (_skillList == null)
        {
            GD.PushError("CharacterDetailSceneController: SkillList is null during ClearSkillList.");
            return;
        }

        foreach (Node child in _skillList.GetChildren())
        {
            _skillList.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static List<SkillViewModel> BuildSkillViewModels(CharacterCatalogEntry character)
    {
        var skillViewModels = new List<SkillViewModel>();
        IReadOnlyList<SkillConfig> skills = character?.Skills;
        if (skills == null)
        {
            return skillViewModels;
        }

        foreach (SkillConfig skill in skills)
        {
            SkillViewModel viewModel = BuildSkillViewModel(skill);
            if (viewModel != null)
            {
                skillViewModels.Add(viewModel);
            }
        }

        return skillViewModels;
    }

    private static SkillViewModel BuildSkillViewModel(SkillConfig skill)
    {
        if (skill == null)
        {
            return null;
        }

        string displayName = skill.SkillName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = GetAdditionalFieldValue(skill, "Name", "name");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = skill.SkillId;
        }

        var viewModel = new SkillViewModel
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "未命名技能" : displayName,
            TypeText = MapSkillType(skill.Type),
            Description = string.IsNullOrWhiteSpace(skill.Description) ? "暂无技能描述" : skill.Description
        };

        viewModel.DetailLines.Add($"类型：{viewModel.TypeText}");
        viewModel.DetailLines.Add(skill.EnergyCost > 0
            ? $"能量消耗：{skill.EnergyCost}"
            : skill.EnergyCost < 0
                ? $"能量回复：{Mathf.Abs(skill.EnergyCost)}"
                : "能量消耗：0");

        AddMetaFromField(skill, viewModel, "倍率", "倍率", "ratio", "Ratio", "multiplier", "Multiplier", "damageMultiplier", "DamageMultiplier");
        AddMetaFromField(skill, viewModel, "效果", "效果", "effect", "Effect", "effectValue", "EffectValue");
        AddMetaFromField(skill, viewModel, "冷却", "冷却", "cooldown", "Cooldown", "cd", "Cd");

        if (!string.IsNullOrWhiteSpace(skill.EnhancedSkillId))
        {
            viewModel.DetailLines.Add($"强化分支：{skill.EnhancedSkillId}");
        }

        if (!string.IsNullOrWhiteSpace(skill.TriggeredBySkillId))
        {
            viewModel.DetailLines.Add($"触发来源：{skill.TriggeredBySkillId}");
        }

        if (!skill.IsSelectable)
        {
            viewModel.DetailLines.Add("不可直接选择");
        }

        if (!skill.ShowInBattleUi)
        {
            viewModel.DetailLines.Add("战斗界面默认隐藏");
        }

        return viewModel;
    }

    private static void AddMetaFromField(SkillConfig skill, SkillViewModel viewModel, string label, params string[] fieldNames)
    {
        string value = GetAdditionalFieldValue(skill, fieldNames);
        if (!string.IsNullOrWhiteSpace(value))
        {
            viewModel.DetailLines.Add($"{label}：{value}");
        }
    }

    private static string GetAdditionalFieldValue(SkillConfig skill, params string[] fieldNames)
    {
        if (skill?.AdditionalFields == null)
        {
            return string.Empty;
        }

        foreach (KeyValuePair<string, JsonElement> additionalField in skill.AdditionalFields)
        {
            foreach (string fieldName in fieldNames)
            {
                if (!string.Equals(additionalField.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return additionalField.Value.ValueKind switch
                {
                    JsonValueKind.String => additionalField.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => additionalField.Value.GetRawText(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => additionalField.Value.GetRawText()
                };
            }
        }

        return string.Empty;
    }

    private void AddSkillEntry(SkillViewModel viewModel)
    {
        if (_skillList == null || viewModel == null)
        {
            return;
        }

        var skillBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        skillBox.AddThemeConstantOverride("separation", 4);

        skillBox.AddChild(CreateSkillLabel(viewModel.DisplayName, 20, SkillTitleColor));
        skillBox.AddChild(CreateSkillLabel(string.Join("  |  ", viewModel.DetailLines), 15, SkillMetaColor));
        skillBox.AddChild(CreateSkillLabel(viewModel.Description, 17, SkillBodyColor));

        _skillList.AddChild(skillBox);
    }

    private void AddEmptySkillState(string message)
    {
        if (_skillList == null)
        {
            GD.PushError("CharacterDetailSceneController: SkillList is null during empty state render.");
            return;
        }

        _skillList.AddChild(CreateSkillLabel(message, 17, SkillBodyColor));
    }

    private static Label CreateSkillLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text ?? string.Empty,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void RefreshSelectButton()
    {
        if (_selectButton == null)
        {
            return;
        }

        _selectButton.Text = CharacterSelectionContext.IsSelected(_character) ? "取消选择" : "选择角色";
    }

    private void OnSelectPressed()
    {
        if (_character == null)
        {
            GoBackToSelection();
            return;
        }

        if (!CharacterSelectionContext.TryToggleCharacter(_character, GlobalScript.MaxPartySize, out _))
        {
            GD.Print("CharacterDetailScene: team is already full.");
        }

        GoBackToSelection();
    }

    private void GoBackToSelection()
    {
        GetTree().ChangeSceneToFile(SelectScenePath);
    }

    private void LogNodeDiagnostics()
    {
        GD.Print(_skillList != null
            ? $"SkillList path={_skillList.GetPath()}, children={_skillList.GetChildCount()}"
            : "SkillList path=<null>");

        GD.Print(_selectButton?.GetParent() != null
            ? $"SelectButton parent={_selectButton.GetParent().GetPath()}"
            : "SelectButton parent=<null>");
    }

    private void LogDeferredLayoutDiagnostics()
    {
        if (_skillScroll != null)
        {
            GD.Print($"SkillScroll size={_skillScroll.Size}, min={_skillScroll.CustomMinimumSize}, visible={_skillScroll.Visible}");
        }

        if (_skillList != null)
        {
            GD.Print($"SkillList size={_skillList.Size}, min={_skillList.CustomMinimumSize}, children={_skillList.GetChildCount()}");
        }
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

    private static string FormatPercent(float value)
    {
        return $"{(value * 100f).ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string MapSkillType(string type)
    {
        if (string.Equals(type, "Passive", StringComparison.OrdinalIgnoreCase))
        {
            return "被动";
        }

        if (string.Equals(type, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            return "普攻";
        }

        if (string.Equals(type, "Special", StringComparison.OrdinalIgnoreCase))
        {
            return "特技";
        }

        if (string.Equals(type, "EnhancedSpecial", StringComparison.OrdinalIgnoreCase))
        {
            return "强化特技";
        }

        if (string.Equals(type, "Ultimate", StringComparison.OrdinalIgnoreCase))
        {
            return "终结技";
        }

        return "技能";
    }
}
