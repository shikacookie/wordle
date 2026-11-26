using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class GameController : Control
{
    private WordRepository _repository;
    private EntropySolver _solver;
    private BoardLogic[] _boards;
    private BoardView _view;
    private BoardToast _boardToast;
    private TactileButton[] _boardButtons;
    private AiHintPanel _hint;
    private Label _status;
    private Label _focusLabel;
    private VirtualKeyboard _keyboard;
    private GameSounds _sounds;
    private static bool _soundEnabled = true;
    private CancellationTokenSource _analysis;
    private bool _isCalculating;
    private bool _analysisQueued;
    // Scene instances can overlap briefly during a restart; only one may run the solver.
    private static readonly SemaphoreSlim AnalysisGate = new(1, 1);
    private static readonly Dictionary<int, GuessRecommendation[]> OpeningHints = new();
    private static bool _checkedOpeningData;
    private static GuessRecommendation[] _singleBoardOpening;
    private int _focus;
    private int _usedGuesses;
    private int _maxGuesses;
    private string _draft = "";
    private bool _ended;
    private bool _assist;
    private bool _sessionReady;

    public override void _Ready()
    {
        _status = GetNodeOrNull<Label>("%Status");
        _keyboard = GetNodeOrNull<VirtualKeyboard>("%Keyboard");
        var menu = GetNodeOrNull<Button>("%Menu");
        var restart = GetNodeOrNull<Button>("%Restart");
        if (menu != null)
            menu.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/start_menu.tscn");
        if (restart != null)
            restart.Pressed += () => GetTree().ReloadCurrentScene();

        try
        {
            Theme = CreamTheme.Create();
            if (GetNodeOrNull<ColorRect>("Background") is ColorRect background)
                background.Color = CreamTheme.Background;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not initialize the game theme: {exception}");
        }

        _assist = GameConfig.EnableAiAssist;
        _maxGuesses = GameConfig.MaxGuesses;
        SetupSound();
        SetupSidebar();
        StartNewSession();
    }

    private void SetupSound()
    {
        var sound = GetNodeOrNull<TactileButton>("%Sound");
        var player = GetNodeOrNull<AudioStreamPlayer>("%ClickAudio");
        if (sound == null || player == null)
            return;
        try
        {
            _sounds = new GameSounds(player, _soundEnabled);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not initialize sound: {exception}");
            return;
        }
        sound.ButtonPressed = _soundEnabled;
        sound.SetCaption(_soundEnabled ? "SOUND: ON" : "SOUND: OFF");
        sound.Toggled += enabled =>
        {
            _soundEnabled = enabled;
            _sounds.SetEnabled(enabled);
            sound.SetCaption(enabled ? "SOUND: ON" : "SOUND: OFF");
        };
    }

    private void SetupSidebar()
    {
        _hint = GetNodeOrNull<AiHintPanel>("%HintPanel");
        if (_hint == null)
        {
            GD.PushWarning("Hint panel is unavailable. Gameplay can still start.");
            return;
        }
        try
        {
            if (_assist && _hint.CanShowSolver)
                _hint.RecommendationPressed += FillRecommendation;
            else if (!_assist && _hint.CanShowSolo)
                _hint.ShowSoloMode(GameConfig.BoardCount, _maxGuesses);
            else
                GD.PushWarning("Hint panel controls are incomplete. Gameplay can still start.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not initialize the sidebar: {exception}");
            _hint = null;
        }
    }

    private void StartNewSession()
    {
        _sessionReady = false;
        try
        {
            _repository = WordRepository.LoadShared();
            if (_repository.Count < GameConfig.BoardCount)
                throw new InvalidOperationException("The word list has fewer words than the number of boards.");
        }
        catch (Exception exception)
        {
            ReportStartupError("WORD LIST COULD NOT BE LOADED.", exception);
            return;
        }

        try
        {
            BuildSession();
            _sessionReady = true;
            SetProgress();
            FocusBoard(0);
        }
        catch (Exception exception)
        {
            ReportStartupError("GAME COULD NOT START.", exception);
            return;
        }
        RefreshRecommendations();
    }

    private void BuildSession()
    {
        _focusLabel = RequireNode<Label>("%FocusLabel");
        if (_keyboard == null)
            throw new InvalidOperationException("Missing scene node %Keyboard.");
        _keyboard.LetterPressed += TypeLetter;
        _keyboard.DeletePressed += DeleteLetter;
        _keyboard.EnterPressed += SubmitGuess;

        _solver = _assist && _hint?.CanShowSolver == true ? new EntropySolver(_repository) : null;
        int count = GameConfig.BoardCount;
        if (GetNodeOrNull<Control>("%PickerSection") is Control pickerSection)
            pickerSection.Visible = count > 1;
        if (GetNodeOrNull<HBoxContainer>("%Workspace") is HBoxContainer workspace)
            workspace.Alignment = count > 1 ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.Center;
        var pool = Enumerable.Range(0, _repository.Count).ToArray();
        _boards = new BoardLogic[count];
        var boardScroll = RequireNode<ScrollContainer>("%BoardScroll");
        var boardGrid = RequireNode<GridContainer>("%BoardGrid");
        var stage = RequireNode<Control>("%BoardStage");
        _view = new BoardView(boardScroll, boardGrid, RequireNode<Label>("%BoardState"), _maxGuesses);
        stage.CustomMinimumSize = boardScroll.CustomMinimumSize;
        _boardToast = new BoardToast(this, boardScroll, boardGrid);
        _boardButtons = new TactileButton[count];
        var picker = GetNodeOrNull<GridContainer>("%BoardPicker");
        for (int i = 0; i < count; i++)
        {
            int choice = Random.Shared.Next(i, pool.Length);
            (pool[i], pool[choice]) = (pool[choice], pool[i]);
            _boards[i] = new BoardLogic(_repository, pool[i]);
            if (count == 1 || picker == null)
                continue;
            int index = i;
            var button = new TactileButton
            {
                Text = $"{i + 1:00}", FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(45, 42)
            };
            button.SetPalette(CreamTheme.Sakura, new Color("F9E2E9"));
            button.Pressed += () => FocusBoard(index);
            _boardButtons[i] = button;
            picker.AddChild(button);
        }
    }

    private T RequireNode<T>(string path) where T : Node =>
        GetNodeOrNull<T>(path) ?? throw new InvalidOperationException($"Missing or invalid scene node {path}.");

    private void ReportStartupError(string message, Exception exception)
    {
        _sessionReady = false;
        if (_status != null)
            _status.Text = message;
        _keyboard?.Hide();
        GD.PushError($"{message} {exception}");
    }

    public override void _ExitTree()
    {
        _analysisQueued = false;
        CancelAnalysis();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
            _keyboard?.ReleasePhysicalKeys();
    }

    public override void _Input(InputEvent input)
    {
        if (!_sessionReady ||
            input is not InputEventKey key)
            return;
        if (!key.Pressed)
        {
            _keyboard.SetPhysicalKey(key);
            return;
        }
        if (key.CtrlPressed || key.AltPressed || key.MetaPressed)
            return;

        if (key.Keycode == Key.Tab)
        {
            if (!key.Echo && _boards.Length > 1)
                FocusBoard(_focus + (key.ShiftPressed ? -1 : 1));
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_ended)
            return;

        _keyboard.SetPhysicalKey(key);
        if (key.Keycode == Key.Backspace)
            DeleteLetter();
        else if (key.Keycode is Key.Enter or Key.KpEnter && !key.Echo)
            SubmitGuess();
        else if (key.Unicode is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            TypeLetter(char.ToUpperInvariant((char)key.Unicode));
        else
            return;
        GetViewport().SetInputAsHandled();
    }

    private bool CanPlay => _sessionReady && !_ended;

    private void FillRecommendation(string word)
    {
        if (!CanPlay)
            return;
        _draft = word;
        _sounds?.PlayLetter();
        SetProgress();
        RenderBoard();
    }

    private void TypeLetter(char letter)
    {
        if (!CanPlay || _draft.Length == GameConfig.WordLength)
            return;
        _draft += letter;
        _sounds?.PlayLetter();
        SetProgress();
        RenderBoard();
    }

    private void DeleteLetter()
    {
        if (!CanPlay || _draft.Length == 0)
            return;
        _draft = _draft[..^1];
        _sounds?.PlayDelete();
        SetProgress();
        RenderBoard();
    }

    private void SubmitGuess()
    {
        if (!CanPlay)
            return;
        if (_draft.Length != GameConfig.WordLength)
        {
            _boardToast.Show("ENTER A FIVE-LETTER WORD.", shake: true);
            return;
        }
        if (!_repository.TryGetIndex(_draft, out int index))
        {
            _boardToast.Show("WORD NOT IN THE LIST.", shake: true);
            return;
        }

        foreach (var board in _boards)
            board.ApplyGuess(index);
        _draft = "";
        _usedGuesses++;
        bool won = _boards.All(board => board.IsSolved);
        _sounds?.PlayAcceptedGuess(won);
        _ended = won || _usedGuesses >= _maxGuesses;
        SetProgress();
        if (_ended)
            _keyboard.Hide();

        if (!_ended && _boards[_focus].IsSolved)
        {
            int next = Array.FindIndex(_boards, _focus, board => !board.IsSolved);
            FocusBoard(next < 0 ? Array.FindIndex(_boards, board => !board.IsSolved) : next);
        }
        else
            FocusBoard(_focus);
        if (_ended)
            _boardToast.Show(won ? "ALL BOARDS SOLVED!" : "OUT OF GUESSES. ANSWERS REVEALED.", shake: false);
        RefreshRecommendations();
    }

    private void FocusBoard(int index)
    {
        if (!_sessionReady)
            return;
        _focus = (index + _boards.Length) % _boards.Length;
        _focusLabel.Text = $"BOARD {_focus + 1:00} / {_boards.Length:00}";
        RenderBoard();
        _keyboard.ShowFeedback(_boards[_focus].History);
        for (int i = 0; i < _boards.Length; i++)
            if (_boardButtons[i] is TactileButton button)
            {
                var fill = _boards[i].IsSolved ? CreamTheme.Mint : i == _focus ? CreamTheme.Peach : CreamTheme.Sakura;
                button.SetPalette(fill, fill.Darkened(0.06f));
            }
        if (_solver != null && _hint?.CanShowSolver == true)
        {
            try
            {
                var board = _boards[_focus];
                var candidates = board.SnapshotCandidates();
                _hint.ShowFocusedBoard(_focus, board.IsSolved, _repository, candidates,
                    _solver.GetLetterProbabilities(candidates));
            }
            catch (Exception exception)
            {
                GD.PushError($"Could not refresh board hints: {exception}");
            }
        }
    }

    private void SetProgress()
    {
        if (_status != null)
        {
            int solved = _boards.Count(board => board.IsSolved);
            _status.Text = $"GUESS {_usedGuesses}/{_maxGuesses}  ·  SOLVED {solved}/{_boards.Length}";
        }
        if (!_assist)
            _hint?.UpdateSoloGuesses(_maxGuesses - _usedGuesses);
    }

    private void RenderBoard()
    {
        var board = _boards[_focus];
        _view.Render(board, _draft, _ended, _assist);
        _view.ScrollToLatest(board, _ended);
    }

    private void CancelAnalysis()
    {
        _analysis?.Cancel();
    }

    private async void RefreshRecommendations()
    {
        if (!_sessionReady || _solver == null || _hint?.CanShowSolver != true)
            return;
        _analysisQueued = true;
        if (_isCalculating)
        {
            CancelAnalysis();
            return;
        }
        _isCalculating = true;
        try
        {
            while (_analysisQueued && IsInsideTree())
            {
                _analysisQueued = false;
                await UpdateRecommendationsAsync();
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not refresh recommendations: {exception}");
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private async Task UpdateRecommendationsAsync()
    {
        if (_ended)
        {
            _hint.SetRecommendationStatus("GAME OVER.");
            return;
        }

        bool opening = _usedGuesses == 0;
        int boardCount = _boards.Length;
        if (opening && !_checkedOpeningData)
        {
            _singleBoardOpening = OpeningRecommendations.TryLoad(_repository);
            _checkedOpeningData = true;
        }
        if (opening && _singleBoardOpening != null && !OpeningHints.ContainsKey(boardCount))
        {
            // All opening boards have the same posterior; each metric scales by the board count.
            OpeningHints[boardCount] = _singleBoardOpening.Select(guess => guess with
            {
                FirstBits = guess.FirstBits * boardCount,
                ExpectedSecondBits = guess.ExpectedSecondBits * boardCount,
                ConvergenceBonus = guess.ConvergenceBonus * boardCount
            }).ToArray();
        }
        if (opening && OpeningHints.TryGetValue(boardCount, out var cached))
        {
            _hint.ShowRecommendations(cached);
            return;
        }

        _hint.SetRecommendationStatus(opening ? "ANALYZING THE WORD LIST…" : "THINKING AHEAD…");
        using var request = new CancellationTokenSource();
        _analysis = request;
        var token = request.Token;
        bool entered = false;
        try
        {
            await AnalysisGate.WaitAsync(token);
            entered = true;
            token.ThrowIfCancellationRequested();
            var candidates = _boards.Where(board => !board.IsSolved).Select(board => board.SnapshotCandidates()).ToArray();
            var result = await Task.Run(() => _solver.Analyze(candidates, token), token);
            // Godot's synchronization context resumes this continuation on the main thread.
            if (token.IsCancellationRequested || !IsInsideTree())
                return;
            if (opening)
                OpeningHints[boardCount] = result;
            _hint.ShowRecommendations(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A guess or scene exit superseded these results.
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested && IsInsideTree())
            {
                _hint.SetRecommendationStatus("COULDN’T FINISH ANALYSIS.");
                GD.PushError(exception.ToString());
            }
        }
        finally
        {
            if (entered)
                AnalysisGate.Release();
            _analysis = null;
        }
    }
}
