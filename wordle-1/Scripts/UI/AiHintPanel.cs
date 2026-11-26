using System;
using System.Linq;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class AiHintPanel : MarginContainer
{
    private const int CandidateLimit = 20;
    private const int RecommendationLimit = 3;
    private const int RecommendationHeight = 46;
    private Label _heading;
    private Label _states;
    private Label _entropy;
    private readonly Label[] _frequentLetters = new Label[6];
    private Label _recommendationStatus;
    private VBoxContainer _recommendations;
    private Control _candidateSection;
    private Control _lettersTitle;
    private GridContainer _letterGrid;
    private readonly Label[] _candidateWords = new Label[CandidateLimit];

    public event Action<string> RecommendationPressed;
    internal bool CanShowSolo => _heading != null && _states != null && _entropy != null && _recommendations != null;
    internal bool CanShowSolver => CanShowSolo && _recommendationStatus != null &&
        _candidateSection != null && _letterGrid != null && _lettersTitle != null &&
        _frequentLetters[^1] != null && _candidateWords[^1] != null;

    public override void _Ready()
    {
        _heading = GetNodeOrNull<Label>("%SolverTitle");
        var badges = GetNodeOrNull<HBoxContainer>("%StateBadges");
        _recommendations = GetNodeOrNull<VBoxContainer>("%Recommendations");
        _recommendationStatus = GetNodeOrNull<Label>("%RecommendationStatus");
        _candidateSection = GetNodeOrNull<Control>("%CandidateSection");
        _letterGrid = GetNodeOrNull<GridContainer>("%FrequentLetters");
        _lettersTitle = GetNodeOrNull<Control>("%TopLettersTitle");
        if (_heading == null || badges == null || _recommendations == null)
        {
            GD.PushError("Hint panel is missing its title, badges, or recommendation container.");
            return;
        }
        _states = AddBadge(badges, new Color("F8E8EE"), "Ω = —");
        _entropy = AddBadge(badges, new Color("F9E9BF"), "S = —");
        if (!GameConfig.EnableAiAssist)
            return;
        var wordGrid = GetNodeOrNull<GridContainer>("%CandidateWords");
        if (_letterGrid == null || _lettersTitle == null || _recommendationStatus == null ||
            _candidateSection == null || wordGrid == null)
        {
            GD.PushError("Hint panel is missing its solver controls.");
            return;
        }
        for (int i = 0; i < _frequentLetters.Length; i++)
        {
            var tag = new PanelContainer { ThemeTypeVariation = "LetterTag", SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(74, 34) };
            var label = new Label { Text = "—", ThemeTypeVariation = "LetterTagLabel", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            tag.AddChild(label);
            _letterGrid.AddChild(tag);
            _frequentLetters[i] = label;
        }
        for (int i = 0; i < _candidateWords.Length; i++)
        {
            var word = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center };
            word.AddThemeFontSizeOverride("font_size", 14);
            wordGrid.AddChild(word);
            _candidateWords[i] = word;
        }
        SetRecommendationStatus("WAITING FOR BOARD…");
    }

    public void ShowSoloMode(int boardCount, int guessesLeft)
    {
        if (!CanShowSolo)
            return;
        _heading.Text = "SOLO MODE";
        _states.Text = $"BOARDS {boardCount:00}";
        UpdateSoloGuesses(guessesLeft);
        _recommendationStatus?.Hide();
        _lettersTitle?.Hide();
        _letterGrid?.Hide();
        _candidateSection?.Hide();
        ClearRecommendations();

        // Occupy the same footprint as the three recommendation keycaps.
        int height = RecommendationLimit * RecommendationHeight
            + (RecommendationLimit - 1) * _recommendations.GetThemeConstant("separation");
        var card = new Control { Name = "SoloChallengeCard", CustomMinimumSize = new Vector2(0, height),
            MouseFilter = MouseFilterEnum.Ignore };
        _recommendations.AddChild(card);
        CreamTheme.AddKeycapLayer(card, CreamTheme.Keycap(CreamTheme.Cocoa), 3);
        var face = CreamTheme.AddKeycapLayer(card, CreamTheme.Keycap(Colors.White), 0);
        var copy = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore };
        face.AddChild(copy);
        copy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        copy.AddThemeConstantOverride("separation", 4);

        var title = new Label { Text = "AI ASSIST OFF", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 21);
        copy.AddChild(title);
        copy.AddChild(new Label { Text = "HUMAN CHALLENGE", ThemeTypeVariation = "MutedLabel",
            HorizontalAlignment = HorizontalAlignment.Center });
    }

    public void UpdateSoloGuesses(int guessesLeft)
    {
        if (_entropy != null)
            _entropy.Text = $"LEFT {guessesLeft:00}";
    }

    public void ShowFocusedBoard(int index, bool solved, WordRepository repository, int[] candidates,
        double[] letterProbabilities)
    {
        string entropy = candidates.Length == 0 ? "—" : FormattableString.Invariant($"{Math.Log2(candidates.Length):F2}");
        _states.Text = FormattableString.Invariant($"Ω = {candidates.Length:N0}");
        _entropy.Text = candidates.Length == 0 ? "S = —" : $"S = {entropy} bits";
        _states.GetParent<Control>().GetParent<Control>().TooltipText =
            $"BOARD {index + 1:00}" + (solved ? " (SOLVED)" : "");
        _candidateSection.Visible = candidates.Length is > 0 and <= CandidateLimit;
        if (_candidateSection.Visible)
        {
            for (int i = 0; i < _candidateWords.Length; i++)
            {
                bool hasWord = i < candidates.Length;
                _candidateWords[i].Visible = hasWord;
                _candidateWords[i].Text = hasWord ? repository.GetWord(candidates[i]) : "";
            }
        }
        var top = Enumerable.Range(0, letterProbabilities.Length)
            .OrderByDescending(i => letterProbabilities[i]).ThenBy(i => i)
            .Take(_frequentLetters.Length).ToArray();
        for (int i = 0; i < top.Length; i++)
            _frequentLetters[i].Text = candidates.Length == 0 ? "—" : FormattableString.Invariant(
                $"{(char)('A' + top[i])} {letterProbabilities[top[i]] * 100:F0}%");
    }

    public void SetRecommendationStatus(string status)
    {
        ClearRecommendations();
        _recommendationStatus.Text = status;
        _recommendationStatus.Show();
    }

    public void ShowRecommendations(GuessRecommendation[] recommendations)
    {
        if (recommendations.Length == 0)
        {
            SetRecommendationStatus("NO SUGGESTIONS YET.");
            return;
        }
        ClearRecommendations();
        _recommendationStatus.Hide();
        foreach (var guess in recommendations.Take(RecommendationLimit))
        {
            string score = guess.ForcedBoardCount > 0 ? "ONLY ANSWER" : FormattableString.Invariant($"{guess.Bits:F2} bits");
            var card = new TactileButton { Text = $"{guess.Word}    {score}",
                FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, RecommendationHeight) };
            card.SetPalette(CreamTheme.Panel, CreamTheme.Panel.Darkened(0.06f));
            card.Pressed += () => RecommendationPressed?.Invoke(guess.Word);
            _recommendations.AddChild(card);
        }
    }

    private void ClearRecommendations()
    {
        foreach (var child in _recommendations.GetChildren())
        {
            _recommendations.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Label AddBadge(HBoxContainer row, Color fill, string text)
    {
        var badge = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44), MouseFilter = MouseFilterEnum.Pass };
        row.AddChild(badge);
        CreamTheme.AddKeycapLayer(badge, CreamTheme.Keycap(CreamTheme.Cocoa), 3);
        var face = CreamTheme.AddKeycapLayer(badge, CreamTheme.Keycap(fill), 0);
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        face.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 17);
        return label;
    }
}
