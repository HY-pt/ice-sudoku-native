using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace IceSudoku;

public partial class MainWindow : Window
{
    private sealed class CellVisual
    {
        public Border Root = null!;
        public TextBlock Value = null!;
        public UniformGrid NotesHost = null!;
        public TextBlock[] Notes = null!;
    }

    private const int MaxRestarts = 3;

    private int[,] _puzzle = new int[9, 9];
    private int[,] _solution = new int[9, 9];
    private int[,] _grid = new int[9, 9];
    private bool[,] _given = new bool[9, 9];
    private readonly HashSet<int>[,] _notes = new HashSet<int>[9, 9];
    private (int R, int C)? _selected;
    private string _difficulty = "medium";
    private string _pendingDiff = "medium";
    private bool _notesMode;
    private int _restarts;
    private int _seconds;
    private bool _over;
    private bool _paused;
    private bool _inGame;
    private bool _maximized;
    private bool _horizontal;
    private Rect _restoreBounds;

    private readonly CellVisual[,] _cells = new CellVisual[9, 9];
    private readonly Button[] _numButtons = new Button[10];
    private readonly TextBlock[] _numVal = new TextBlock[10];
    private readonly TextBlock[] _numLeft = new TextBlock[10];
    private readonly TextBlock[] _numBadge = new TextBlock[10];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private static Brush B(string key) => (Brush)Application.Current.Resources[key];

    public MainWindow()
    {
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) _notes[r, c] = new HashSet<int>();
        InitializeComponent();
        BuildBoard();
        BuildNumpad();
        _timer.Tick += (_, _) => { if (!_over && !_paused && _inGame) { _seconds++; UpdateTimer(); } };
        SourceInitialized += (_, _) => InitNative();
        UpdateThemeIcon();
    }

    /* ---------- 棋盘构建 ---------- */
    private void BuildBoard()
    {
        for (int i = 0; i < 9; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            var root = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var inner = new Grid();

            var value = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("NumFont"),
                FontSize = 30,
                FontWeight = FontWeights.SemiBold
            };

            var notesHost = new UniformGrid { Columns = 3, Rows = 3, Margin = new Thickness(2), Visibility = Visibility.Collapsed };
            var notes = new TextBlock[9];
            for (int n = 0; n < 9; n++)
            {
                notes[n] = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                notesHost.Children.Add(notes[n]);
            }

            inner.Children.Add(notesHost);
            inner.Children.Add(value);

            if (c != 8)
            {
                bool thick = c == 2 || c == 5;
                var line = new Rectangle { Width = thick ? 2 : 1, HorizontalAlignment = HorizontalAlignment.Right };
                line.SetResourceReference(Shape.FillProperty, thick ? "GridBoldBrush" : "GridLineBrush");
                inner.Children.Add(line);
            }
            if (r != 8)
            {
                bool thick = r == 2 || r == 5;
                var line = new Rectangle { Height = thick ? 2 : 1, VerticalAlignment = VerticalAlignment.Bottom };
                line.SetResourceReference(Shape.FillProperty, thick ? "GridBoldBrush" : "GridLineBrush");
                inner.Children.Add(line);
            }

            root.Child = inner;
            int rr = r, cc = c;
            root.MouseLeftButtonUp += (_, _) => SelectCell(rr, cc);
            Grid.SetRow(root, r);
            Grid.SetColumn(root, c);
            BoardGrid.Children.Add(root);
            _cells[r, c] = new CellVisual { Root = root, Value = value, NotesHost = notesHost, Notes = notes };
        }
    }

    private void BuildNumpad()
    {
        for (int n = 1; n <= 9; n++)
        {
            var btn = new Button { Style = (Style)FindResource("NumButton"), Margin = new Thickness(3) };
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            var val = new TextBlock { Text = n.ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var sub = new TextBlock { FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) };
            var badge = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 6, 0), Visibility = Visibility.Collapsed };
            g.Children.Add(val);
            g.Children.Add(sub);
            g.Children.Add(badge);
            btn.Content = g;
            int nn = n;
            btn.Click += (_, _) => InputNumber(nn);
            NumpadGrid.Children.Add(btn);
            _numButtons[n] = btn;
            _numVal[n] = val;
            _numLeft[n] = sub;
            _numBadge[n] = badge;
        }
    }

    /* ---------- 新游戏 ---------- */
    private async Task NewGameAsync(string? diff)
    {
        if (diff != null) _difficulty = diff;
        SetHint("", null);
        Overlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        HomePanel.Visibility = Visibility.Collapsed;
        GamePanel.Visibility = Visibility.Visible;
        _over = false;
        _paused = false;
        _inGame = true;
        PauseBtn.IsEnabled = false;

        var puzzle = await Task.Run(() => SudokuEngine.MakePuzzle(_difficulty));

        _puzzle = puzzle.Grid;
        _solution = puzzle.Solution;
        _grid = SudokuEngine.Clone(puzzle.Grid);
        _given = new bool[9, 9];
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            _given[r, c] = puzzle.Grid[r, c] != 0;
            _notes[r, c].Clear();
        }
        _selected = null;
        _restarts = 0;
        _seconds = 0;
        _notesMode = false;
        NotesToggle.IsChecked = false;
        RestartBtn.IsEnabled = true;
        RestartText.Text = $"0 / {MaxRestarts}";
        DiffText.Text = SudokuEngine.DifficultyLabel[_difficulty];
        UpdateTimer();
        _timer.Stop();
        _timer.Start();
        SelectFirstEmpty();
        Render();
        PauseBtn.IsEnabled = true;
    }

    private void SelectFirstEmpty()
    {
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
            if (_grid[r, c] == 0) { _selected = (r, c); return; }
    }

    private void UpdateTimer()
    {
        TimerText.Text = $"{_seconds / 60:00}:{_seconds % 60:00}";
    }

    /* ---------- 选择 ---------- */
    private void SelectCell(int r, int c)
    {
        if (_over || _paused || !_inGame) return;
        _selected = (r, c);
        Render();
    }

    /* ---------- 填数 / 草稿 ---------- */
    private void InputNumber(int n)
    {
        if (_over || _paused || !_inGame || _selected is not { } sel) return;
        int r = sel.R, c = sel.C;
        if (_given[r, c]) return;

        if (_notesMode)
        {
            if (_grid[r, c] != 0) return;
            if (!_notes[r, c].Add(n)) _notes[r, c].Remove(n);
            Render();
            return;
        }

        _grid[r, c] = n;
        _notes[r, c].Clear();
        Render();
        CheckWin();
    }

    private void Erase()
    {
        if (_over || _paused || !_inGame || _selected is not { } sel) return;
        int r = sel.R, c = sel.C;
        if (_given[r, c]) return;
        _grid[r, c] = 0;
        _notes[r, c].Clear();
        Render();
    }

    private void Restart()
    {
        if (_over || _paused || !_inGame || _restarts >= MaxRestarts) return;
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            if (_given[r, c]) continue;
            _grid[r, c] = 0;
            _notes[r, c].Clear();
        }
        _restarts++;
        RestartText.Text = $"{_restarts} / {MaxRestarts}";
        RestartBtn.IsEnabled = _restarts < MaxRestarts;
        SetHint($"盘面已清空，第 {_restarts} 次重来。", null);
        Render();
    }

    /* ---------- 冲突检测 ---------- */
    private bool[,] ComputeConflicts()
    {
        var bad = new bool[9, 9];
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            int v = _grid[r, c];
            if (v == 0) continue;
            for (int i = 0; i < 9; i++)
            {
                if (i != c && _grid[r, i] == v) { bad[r, c] = true; bad[r, i] = true; }
                if (i != r && _grid[i, c] == v) { bad[r, c] = true; bad[i, c] = true; }
            }
            int br = r / 3 * 3, bc = c / 3 * 3;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                int rr = br + i, cc = bc + j;
                if ((rr != r || cc != c) && _grid[rr, cc] == v) { bad[r, c] = true; bad[rr, cc] = true; }
            }
        }
        return bad;
    }

    /* ---------- 渲染 ---------- */
    private void Render()
    {
        var conflicts = ComputeConflicts();
        var sel = _selected;
        int sameVal = sel is { } s0 ? _grid[s0.R, s0.C] : 0;

        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            var cell = _cells[r, c];
            int v = _grid[r, c];
            Brush bg = Brushes.Transparent;
            if (sel is { } s)
            {
                bool sameBox = r / 3 == s.R / 3 && c / 3 == s.C / 3;
                if (r == s.R || c == s.C || sameBox) bg = B("PeerBrush");
                if (sameVal != 0 && v == sameVal && !(r == s.R && c == s.C)) bg = B("SameBrush");
                if (r == s.R && c == s.C) bg = B("SelectedBrush");
            }
            if (conflicts[r, c]) bg = B("DangerSubtleBrush");
            cell.Root.Background = bg;

            if (v != 0)
            {
                cell.Value.Text = v.ToString();
                cell.Value.Foreground = _notesMode ? B("NoteBrush") : (conflicts[r, c] ? B("DangerBrush") : (_given[r, c] ? B("GivenBrush") : B("UserBrush")));
                cell.Value.FontWeight = _given[r, c] ? FontWeights.Bold : FontWeights.SemiBold;
                cell.NotesHost.Visibility = Visibility.Collapsed;
            }
            else if (_notes[r, c].Count > 0)
            {
                cell.Value.Text = "";
                cell.NotesHost.Visibility = Visibility.Visible;
                for (int n = 1; n <= 9; n++)
                {
                    cell.Notes[n - 1].Text = _notes[r, c].Contains(n) ? n.ToString() : "";
                    cell.Notes[n - 1].Foreground = B("NoteBrush");
                }
            }
            else
            {
                cell.Value.Text = "";
                cell.NotesHost.Visibility = Visibility.Collapsed;
            }
        }
        UpdateNumpad();
    }

    private void UpdateNumpad()
    {
        var counts = new int[10];
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) { int v = _grid[r, c]; if (v != 0) counts[v]++; }
        for (int n = 1; n <= 9; n++)
        {
            int left = 9 - counts[n];
            _numVal[n].Text = n.ToString();
            if (_notesMode)
            {
                _numVal[n].Foreground = B("NoteBrush");
                _numLeft[n].Visibility = Visibility.Collapsed;
                _numBadge[n].Text = n.ToString();
                _numBadge[n].Foreground = B("TextPrimaryBrush");
                _numBadge[n].Visibility = left > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _numVal[n].Foreground = B("TextPrimaryBrush");
                _numLeft[n].Text = left > 0 ? left.ToString() : "";
                _numLeft[n].Foreground = B("TextTertiaryBrush");
                _numLeft[n].Visibility = Visibility.Visible;
                _numBadge[n].Visibility = Visibility.Collapsed;
            }
            _numButtons[n].Opacity = left <= 0 ? 0.35 : 1.0;
            _numButtons[n].IsEnabled = left > 0;
        }
    }

    /* ---------- 提示 ---------- */
    private void Hint()
    {
        if (_over || _paused || !_inGame) return;
        var cands = SudokuEngine.GetCandidates(_grid);
        var step = SudokuEngine.ScanUnits(_grid, cands, 4);
        int r, c;
        if (step != null)
        {
            r = step.Row; c = step.Col;
            SetHint($"提示 · {step.Technique}：{step.Explain}", "ok");
        }
        else
        {
            var empties = new List<(int, int)>();
            for (int i = 0; i < 9; i++) for (int j = 0; j < 9; j++) if (_grid[i, j] == 0) empties.Add((i, j));
            if (empties.Count == 0) return;
            var pick = empties[Random.Shared.Next(empties.Count)];
            r = pick.Item1; c = pick.Item2;
            SetHint("当前没有可立即推导的步，已为你揭示一格作为线索。", "ok");
        }
        _grid[r, c] = _solution[r, c];
        _given[r, c] = true;
        _notes[r, c].Clear();
        _selected = (r, c);
        Render();
        CheckWin();
    }

    private void Check()
    {
        if (_over || _paused || !_inGame) return;
        int wrong = 0;
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
            if (_grid[r, c] != 0 && _grid[r, c] != _solution[r, c]) wrong++;
        if (wrong == 0) SetHint("目前填的都正确 👍", "ok");
        else SetHint($"还有 {wrong} 格与正解不符，红框处需修正。", "warn");
    }

    private void Solve()
    {
        if (_over || _paused || !_inGame) return;
        _grid = SudokuEngine.Clone(_solution);
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) { _given[r, c] = true; _notes[r, c].Clear(); }
        Render();
        Win();
    }

    /* ---------- 胜负 ---------- */
    private void CheckWin()
    {
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
            if (_grid[r, c] == 0) return;
        var bad = ComputeConflicts();
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
            if (bad[r, c]) return;
        Win();
    }

    private void Win()
    {
        _over = true;
        _timer.Stop();
        OverlayEmoji.Text = "🎉";
        OverlayTitle.Text = "完成！";
        OverlayText.Text = $"用时 {TimerText.Text}，重来 {_restarts} 次。漂亮！";
        Overlay.Visibility = Visibility.Visible;
    }

    /* ---------- 提示条 ---------- */
    private void SetHint(string msg, string? kind)
    {
        HintText.Text = msg;
        HintText.Foreground = kind switch
        {
            "ok" => B("SuccessBrush"),
            "warn" => B("WarnBrush"),
            _ => B("TextSecondaryBrush")
        };
        HintBar.BorderBrush = kind switch
        {
            "ok" => B("SuccessBrush"),
            "warn" => B("WarnBrush"),
            _ => B("CardBorderBrush")
        };
    }

    /* ---------- 主题 ---------- */
    private void ToggleTheme()
    {
        App.SetTheme(!App.IsDark);
        UpdateThemeIcon();
        Render();
    }

    private void UpdateThemeIcon()
    {
        MoonIcon.Visibility = App.IsDark ? Visibility.Collapsed : Visibility.Visible;
        SunIcon.Visibility = App.IsDark ? Visibility.Visible : Visibility.Collapsed;
    }

    /* ---------- 布局切换 ---------- */
    private static void MoveTo(FrameworkElement e, Panel p, int row = -1)
    {
        (e.Parent as Panel)?.Children.Remove(e);
        if (row >= 0) Grid.SetRow(e, row);
        p.Children.Add(e);
    }

    private void LayoutToggle_Click(object sender, RoutedEventArgs e) => SetLayout(!_horizontal);

    private void SetLayout(bool h)
    {
        if (h == _horizontal) return;
        _horizontal = h;
        if (h)
        {
            VRoot.Visibility = Visibility.Collapsed;
            HRoot.Visibility = Visibility.Visible;
            MoveTo(BoardHost, HBoard);
            BoardHost.Margin = new Thickness(0);
            MoveTo(StatsHost, HTop);
            StatsHost.Margin = new Thickness(0, 0, 0, 10);
            MoveTo(NumpadGrid, HSide, 0);
            NumpadGrid.Columns = 3; NumpadGrid.Rows = 3;
            NumpadGrid.Margin = new Thickness(0, 0, 0, 12);
            for (int n = 1; n <= 9; n++) _numButtons[n].Height = double.NaN;
            MoveTo(ActionsHost, HSide, 1);
            ActionsHost.Columns = 2; ActionsHost.Rows = 3;
            ActionsHost.Height = 132;
            ActionsHost.Margin = new Thickness(0, 0, 0, 12);
            MoveTo(HintBar, HSide, 2);
            HintBar.Margin = new Thickness(0, 0, 0, 10);
            MoveTo(FooterText, HSide, 3);
            FooterText.TextWrapping = TextWrapping.Wrap;
            FooterText.Margin = new Thickness(0, 6, 0, 0);
            MinWidth = 900; MinHeight = 680;
            if (_maximized) _restoreBounds = new Rect(_restoreBounds.X, _restoreBounds.Y, 960, 780);
            else { Width = 960; Height = 780; }
        }
        else
        {
            HRoot.Visibility = Visibility.Collapsed;
            VRoot.Visibility = Visibility.Visible;
            MoveTo(BoardHost, VRoot, 1);
            BoardHost.Margin = new Thickness(0, 0, 0, 14);
            MoveTo(StatsHost, VRoot, 0);
            StatsHost.Margin = new Thickness(0, 0, 0, 14);
            MoveTo(NumpadGrid, VRoot, 2);
            NumpadGrid.Columns = 9; NumpadGrid.Rows = 1;
            NumpadGrid.Margin = new Thickness(0, 0, 0, 12);
            for (int n = 1; n <= 9; n++) _numButtons[n].Height = 56;
            MoveTo(ActionsHost, VRoot, 3);
            ActionsHost.Columns = 6; ActionsHost.Rows = 1;
            ActionsHost.Height = double.NaN;
            MoveTo(HintBar, VRoot, 4);
            HintBar.Margin = new Thickness(0, 0, 0, 10);
            MoveTo(FooterText, VRoot, 5);
            FooterText.TextWrapping = TextWrapping.NoWrap;
            FooterText.Margin = new Thickness(0, 0, 0, 10);
            MinWidth = 620; MinHeight = 860;
            if (_maximized) _restoreBounds = new Rect(_restoreBounds.X, _restoreBounds.Y, 660, 940);
            else { Width = 660; Height = 940; }
        }
    }

    /* ---------- 主菜单 / 暂停 ---------- */
    private void StartGame_Click(object sender, RoutedEventArgs e) => _ = NewGameAsync(_difficulty);
    private void ExitGame_Click(object sender, RoutedEventArgs e) => Close();

    private void PauseGame()
    {
        if (!_inGame || _over || _paused) return;
        _paused = true;
        _timer.Stop();
        _pendingDiff = _difficulty;
        PDiffEasy.IsChecked = _difficulty == "easy";
        PDiffMedium.IsChecked = _difficulty == "medium";
        PDiffHard.IsChecked = _difficulty == "hard";
        PDiffExpert.IsChecked = _difficulty == "expert";
        PauseOverlay.Visibility = Visibility.Visible;
    }

    private void ResumeGame()
    {
        if (!_paused) return;
        _paused = false;
        PauseOverlay.Visibility = Visibility.Collapsed;
        if (!_over) _timer.Start();
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => PauseGame();
    private void Resume_Click(object sender, RoutedEventArgs e) => ResumeGame();
    private void PauseNew_Click(object sender, RoutedEventArgs e)
    {
        PauseOverlay.Visibility = Visibility.Collapsed;
        _paused = false;
        _ = NewGameAsync(_pendingDiff);
    }
    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        PauseOverlay.Visibility = Visibility.Collapsed;
        _paused = false;
        _inGame = false;
        _timer.Stop();
        GamePanel.Visibility = Visibility.Collapsed;
        HomePanel.Visibility = Visibility.Visible;
    }
    private void PauseDiff_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string d) _pendingDiff = d;
    }

    /* ---------- 事件 ---------- */
    private void ThemeToggle_Click(object sender, RoutedEventArgs e) => ToggleTheme();
    private void Erase_Click(object sender, RoutedEventArgs e) => Erase();
    private void Hint_Click(object sender, RoutedEventArgs e) => Hint();
    private void Check_Click(object sender, RoutedEventArgs e) => Check();
    private void Restart_Click(object sender, RoutedEventArgs e) => Restart();
    private void Solve_Click(object sender, RoutedEventArgs e) => Solve();
    private void NotesToggle_Changed(object sender, RoutedEventArgs e)
    {
        _notesMode = NotesToggle.IsChecked == true;
        if (_inGame) Render();
    }
    private void OverlayNew_Click(object sender, RoutedEventArgs e) => _ = NewGameAsync(null);
    private void OverlayDismiss_Click(object sender, RoutedEventArgs e) => Overlay.Visibility = Visibility.Collapsed;

    /* ---------- 标题栏 ---------- */
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => SetMaximized(!_maximized);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxGlyph()
    {
        MaxBtn.Content = _maximized ? "❐" : "□";
        MaxBtn.ToolTip = _maximized ? "还原" : "最大化";
    }

    /* ---------- 原生窗口：手动最大化 + Win11 圆角 ---------- */
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MAXIMIZE = 0xF030;
    private const int SC_RESTORE = 0xF120;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFO mi);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    private IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    private void InitNative()
    {
        HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
        SetCorners(true);
        UpdateMaxGlyph();
    }

    private IntPtr WndProc(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND)
        {
            int cmd = (int)(w.ToInt64() & 0xFFF0);
            if (cmd == SC_MAXIMIZE) { SetMaximized(true); handled = true; }
            else if (cmd == SC_RESTORE) { SetMaximized(false); handled = true; }
        }
        return IntPtr.Zero;
    }

    private void SetCorners(bool round)
    {
        int pref = round ? 2 : 1; // DWMWCP_ROUND / DWMWCP_DONOTROUND
        DwmSetWindowAttribute(Hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
    }

    private Rect WorkArea()
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(Hwnd, MONITOR_DEFAULTTONEAREST), ref mi);
        return new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
    }

    // 不用 WindowState.Maximized（WindowChrome 会留一圈透明壳），直接 SetWindowPos 到工作区
    private void SetMaximized(bool max)
    {
        if (max == _maximized) return;
        if (max)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            Rect wa = WorkArea();
            SetWindowPos(Hwnd, IntPtr.Zero, (int)wa.X, (int)wa.Y, (int)wa.Width, (int)wa.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        else
        {
            SetWindowPos(Hwnd, IntPtr.Zero, (int)_restoreBounds.X, (int)_restoreBounds.Y, (int)_restoreBounds.Width, (int)_restoreBounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        _maximized = max;
        SetCorners(!max);
        UpdateMaxGlyph();
    }

    /* ---------- 键盘 ---------- */
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            if (_inGame && !_over) { if (_paused) ResumeGame(); else PauseGame(); }
            e.Handled = true;
            return;
        }
        if (_paused || !_inGame) return;
        if (_selected is not { } sel) return;
        int r = sel.R, c = sel.C;

        int digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => 0
        };
        if (digit != 0) { InputNumber(digit); e.Handled = true; return; }

        if (e.Key is Key.Back or Key.Delete or Key.D0 or Key.NumPad0) { Erase(); e.Handled = true; return; }
        if (e.Key == Key.N) { NotesToggle.IsChecked = !NotesToggle.IsChecked; e.Handled = true; return; }

        int nr = r, nc = c;
        switch (e.Key)
        {
            case Key.Up: nr = Math.Max(0, r - 1); break;
            case Key.Down: nr = Math.Min(8, r + 1); break;
            case Key.Left: nc = Math.Max(0, c - 1); break;
            case Key.Right: nc = Math.Min(8, c + 1); break;
            default: return;
        }
        e.Handled = true;
        SelectCell(nr, nc);
    }
}