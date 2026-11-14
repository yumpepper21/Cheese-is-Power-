using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace CheeseTowerDefense;

// ============ 地图与实体定义 ============

public enum TileType { Wall, Ground, Spawn, Base }

public enum ToolType { None, Farmer, Police, Guard, Stop }

/// <summary>可放在墙上的建筑（农民 / 警察 / 卫兵）</summary>
public abstract class Building
{
    public abstract string Emoji { get; }
    public abstract string Name { get; }
}

/// <summary>🧔农民：每5秒稳定产出25芝士</summary>
public class Farmer : Building
{
    public double Timer;             // 距下一次产出的进度
    public const int Interval = 5;   // 每5秒产出一批
    public const int Amount = 25;    // 产出25芝士
    public override string Emoji => "🧔";
    public override string Name => "农民";

    /// <summary>返回本次产出的芝士数量（到点即产，无概率）</summary>
    public int Tick(double dt)
    {
        Timer += dt;
        if (Timer < Interval) return 0;
        Timer -= Interval;
        return Amount;
    }
}

/// <summary>炮台（👮警察 / 💂卫兵）</summary>
public class Turret : Building
{
    public double Cooldown;
    public double Dmg;
    public double FireRate;
    public double Range;
    public int BulletsPerShot;
    private readonly string _emoji;
    private readonly string _name;

    public Turret(string emoji, string name, double dmg, double fireRate, double range, int bullets, double knockback = 0)
    {
        _emoji = emoji; _name = name;
        Dmg = dmg; FireRate = fireRate; Range = range; BulletsPerShot = bullets; Knockback = knockback;
    }

    public double Knockback;

    public override string Emoji => _emoji;
    public override string Name => _name;
}

/// <summary>✋停止：放在地面上的路障，耐久25，怪物每秒破坏1点</summary>
public class StopBarrier
{
    public int Hp = 25;
    public const int MaxHp = 25;
}

/// <summary>😈怪物：血量25，寻路走向玩家基地</summary>
public class Monster
{
    public const int MaxHp = 25;
    public double X, Y;
    public int Hp = MaxHp;
    public double Speed;
    public double AtkTimer;
    public bool Attacking;

    public Monster(double x, double y, double speed) { X = x; Y = y; Speed = speed; }
}

/// <summary>子弹（附带轻微击退）</summary>
public class Bullet
{
    public double X, Y;
    public double Vx, Vy;
    public double Dmg;
    public double Knockback;     // 命中后击退的格数
    public Monster? Target;
    public double Life = 2.0;
    public const double Speed = 9.0;

    public Bullet(double x, double y, Monster target, double dmg, double knockback)
    {
        X = x; Y = y; Target = target; Dmg = dmg; Knockback = knockback;
        double dx = target.X - x, dy = target.Y - y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001) len = 1;
        Vx = dx / len * Speed; Vy = dy / len * Speed;
    }
}

/// <summary>飘字提示</summary>
public class FloatText
{
    public string Text;
    public double X, Y;
    public double Life = 1.2;
    public double MaxLife = 1.2;
    public Color Color;

    public FloatText(string text, double x, double y, Color color, double life = 1.2)
    { Text = text; X = x; Y = y; Color = color; Life = life; MaxLife = life; }
}

/// <summary>双缓冲画板，解决闪烁</summary>
public class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; }
}

// ============ 主窗体：芝士就是力量 ============

public class GameForm : Form
{
    // ---- 地图常量：20x10，墙块间留3格空隙 ----
    public const int Cols = 20, Rows = 10;
    public const int Cell = 48;
    private readonly TileType[,] _map = new TileType[Rows, Cols];
    private readonly Building?[,] _buildings = new Building?[Rows, Cols];
    private readonly StopBarrier?[,] _stops = new StopBarrier?[Rows, Cols];
    private double[,] _dist = new double[Rows, Cols];

    public (int r, int c) SpawnCell = (4, 0);   // 左侧红点
    public (int r, int c) BaseCell = (4, 19);   // 右侧绿点（玩家本体）

    // ---- 游戏状态 ----
    private int _cheese = 50;
    private int _playerHp = 3;
    private bool _gameOver;
    private double _elapsed;
    private int _wave = 1;
    private double _waveTimer;
    private double _spawnTimer;
    private readonly List<Monster> _monsters = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<FloatText> _floats = new();
    private readonly Random _rnd = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTime;

    // ---- UI ----
    private ToolType _tool = ToolType.None;
    private readonly BufferedPanel _board = new();
    private System.Windows.Forms.Timer _gameTimer = null!;
    private Label _lblCheese = null!, _lblHp = null!, _lblWave = null!, _lblTip = null!;
    private readonly Dictionary<ToolType, Button> _toolButtons = new();

    // ---- 预创建画笔，避免每帧分配导致闪烁 ----
    private static readonly Font EmojiFont = new("Segoe UI Emoji", 17f);
    private static readonly Font SmallFont = new("Segoe UI", 9f);
    private static readonly Font BigFont = new("Segoe UI", 32f, FontStyle.Bold);
    private static readonly Brush WallBrush = new SolidBrush(Color.FromArgb(58, 58, 64));
    private static readonly Brush GroundBrush = new SolidBrush(Color.FromArgb(32, 32, 37));
    private static readonly Brush SceneBrush = new SolidBrush(Color.FromArgb(16, 16, 18));
    private static readonly Pen WallPen = new(Color.FromArgb(95, 95, 102));
    private static readonly Pen GroundPen = new(Color.FromArgb(42, 42, 48));
    private static readonly Pen OkPen = new(Color.FromArgb(120, 230, 120), 2);
    private static readonly Pen BadPen = new(Color.FromArgb(220, 80, 80), 2);

    public GameForm()
    {
        Text = "芝士就是力量 —— 🧔⚔😈 塔防";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 24, 28);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)!; } catch { /* 图标加载失败不影响游戏 */ }

        int boardW = Cols * Cell, boardH = Rows * Cell;
        ClientSize = new Size(boardW + 20 + 190, boardH + 20);

        _board.Location = new Point(10, 10);
        _board.Size = new Size(boardW, boardH);
        _board.BackColor = Color.FromArgb(16, 16, 18);
        _board.Paint += Board_Paint;
        _board.MouseClick += Board_MouseClick;
        _board.MouseMove += (s, e) => _board.Invalidate();
        Controls.Add(_board);

        BuildSidebar(boardW);

        InitMap();
        ComputeDistField();

        _gameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _gameTimer.Tick += (s, e) =>
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, now - _lastTime); // 真实经过时间
            _lastTime = now;
            UpdateGame(dt);
            _board.Invalidate();
        };
        _gameTimer.Start();
    }

    // ---- 地图初始化：外圈墙 + 内部6个墙块（3上3下），块间留3格空隙 ----
    private void InitMap()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _map[r, c] = (r == 0 || r == Rows - 1 || c == 0 || c == Cols - 1)
                    ? TileType.Wall : TileType.Ground;

        // 墙块3宽x2高，列起点 {2, 8, 14}：块间空隙为 5~7、11~13 列
        // 上排块位于第2~3行（与顶部墙之间留第1行空隙），下排块位于第6~7行（与底部墙之间留第8行空隙）
        int[] blockCols = { 2, 8, 14 };
        foreach (int c0 in blockCols)
        {
            for (int r = 2; r <= 3; r++)
                for (int c = c0; c < c0 + 3; c++)
                    _map[r, c] = TileType.Wall;
            for (int r = 6; r <= 7; r++)
                for (int c = c0; c < c0 + 3; c++)
                    _map[r, c] = TileType.Wall;
        }

        // 中间走廊：第4、5行保持地面（红点→绿点天然通路）
        for (int c = 1; c < Cols - 1; c++) { _map[4, c] = TileType.Ground; _map[5, c] = TileType.Ground; }

        _map[SpawnCell.r, SpawnCell.c] = TileType.Spawn;
        _map[BaseCell.r, BaseCell.c] = TileType.Base;
    }

    // ---- Dijkstra加权距离场：路障格成本=1步+剩余耐久，有绕路时怪物优先绕行 ----
    private void ComputeDistField()
    {
        _dist = new double[Rows, Cols];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++) _dist[r, c] = double.PositiveInfinity;

        _dist[BaseCell.r, BaseCell.c] = 0;
        var done = new bool[Rows, Cols];
        int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };

        // 网格很小（200格），直接选择式Dijkstra
        for (int n = 0; n < Rows * Cols; n++)
        {
            int ur = -1, uc = -1;
            double best = double.PositiveInfinity;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (!done[r, c] && _dist[r, c] < best) { best = _dist[r, c]; ur = r; uc = c; }
            if (ur == -1) break;
            done[ur, uc] = true;
            for (int i = 0; i < 4; i++)
            {
                int nr = ur + dr[i], nc = uc + dc[i];
                if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) continue;
                if (_map[nr, nc] == TileType.Wall) continue;
                double cost = 1 + (_stops[nr, nc] != null ? _stops[nr, nc]!.Hp : 0); // 路障=1步+耐久消耗
                if (_dist[ur, uc] + cost < _dist[nr, nc]) _dist[nr, nc] = _dist[ur, uc] + cost;
            }
        }
    }

    // ---- 侧栏 ----
    private void BuildSidebar(int boardW)
    {
        int x = boardW + 30;
        var title = new Label
        {
            Text = "🧀 芝士就是力量",
            Font = new Font("Segoe UI Emoji", 13f, FontStyle.Bold),
            ForeColor = Color.Gold,
            Location = new Point(x, 12),
            AutoSize = true
        };
        Controls.Add(title);

        _lblCheese = MakeLabel(x, 50, Color.Khaki);
        _lblHp = MakeLabel(x, 78, Color.Tomato);
        _lblWave = MakeLabel(x, 106, Color.LightGreen);
        _lblTip = new Label
        {
            Text = "",
            Font = SmallFont,
            ForeColor = Color.Gainsboro,
            Location = new Point(x, 136),
            Size = new Size(180, 46)
        };
        Controls.Add(_lblTip);

        AddToolButton(ToolType.Farmer, "🧔 农民   🧀50", "放在墙上，每5秒产出25芝士", 196);
        AddToolButton(ToolType.Police, "👮 警察   🧀100", "基础炮台，单发+轻微击退", 244);
        AddToolButton(ToolType.Guard, "💂 卫兵   🧀175", "进阶炮台，2连发+强击退", 292);
        AddToolButton(ToolType.Stop, "✋ 停止   🧀25", "放在地面挡路，耐久25", 340);

        var restart = new Button
        {
            Text = "🔄 重新开始",
            Location = new Point(x, 404),
            Size = new Size(170, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 68),
            ForeColor = Color.White
        };
        restart.FlatAppearance.BorderSize = 0;
        restart.Click += (s, e) => Restart();
        Controls.Add(restart);
    }

    private Label MakeLabel(int x, int y, Color color) =>
        new() { Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI Emoji", 11f, FontStyle.Bold), ForeColor = color };

    private void AddToolButton(ToolType tool, string text, string tip, int y)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(_board.Width + 30 - _board.Left, y),
            Size = new Size(170, 40),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 52),
            ForeColor = Color.White,          // 买得起=白色
            Font = new Font("Segoe UI Emoji", 10f, FontStyle.Bold),
            Tag = tip
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (s, e) => SelectTool(tool);
        Controls.Add(b);
        _toolButtons[tool] = b;
    }

    private void SelectTool(ToolType tool)
    {
        int cost = ToolCost(tool);
        if (tool != ToolType.None && _cheese < cost) { Tip("🧀 芝士不足！"); return; }
        _tool = _tool == tool ? ToolType.None : tool;
        UpdateButtons();
        _lblTip.Text = _tool switch
        {
            ToolType.Farmer => "点击【墙】放置农民",
            ToolType.Police => "点击【墙】放置警察",
            ToolType.Guard => "点击【墙】放置卫兵",
            ToolType.Stop => "点击【地面】放置停止",
            _ => ""
        };
    }

    private void UpdateButtons()
    {
        foreach (var kv in _toolButtons)
        {
            // 芝士足够=白字高亮，不足=灰字
            bool afford = _cheese >= ToolCost(kv.Key);
            kv.Value.ForeColor = afford ? Color.White : Color.Gray;
            kv.Value.BackColor = kv.Key == _tool ? Color.FromArgb(70, 95, 65) : Color.FromArgb(45, 45, 52);
        }
    }

    private static int ToolCost(ToolType t) => t switch
    {
        ToolType.Farmer => 50,
        ToolType.Police => 100,
        ToolType.Guard => 175,
        ToolType.Stop => 25,
        _ => 0
    };

    // ---- 放置 ----
    private void Board_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_gameOver || _tool == ToolType.None) return;
        int c = e.X / Cell, r = e.Y / Cell;
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;

        var tile = _map[r, c];
        int cost = ToolCost(_tool);

        if (_tool == ToolType.Stop)
        {
            if (tile != TileType.Ground) { Tip("✋ 只能放在地面上！"); return; }
            if (_stops[r, c] != null) { Tip("这里已有停止"); return; }
            if (_cheese < cost) { Tip("🧀 芝士不足！"); return; }
            _cheese -= cost;
            _stops[r, c] = new StopBarrier();
            ComputeDistField(); // 新路障改变权重，重新计算距离场
            AddFloat(r, c, "✋", Color.White);
        }
        else
        {
            if (tile != TileType.Wall) { Tip("只能放在【墙】上！"); return; }
            if (_buildings[r, c] != null) { Tip("这里已有建筑"); return; }
            if (_cheese < cost) { Tip("🧀 芝士不足！"); return; }
            _cheese -= cost;
            _buildings[r, c] = _tool switch
            {
                ToolType.Farmer => new Farmer(),
                ToolType.Police => new Turret("👮", "警察", dmg: 5, fireRate: 1.0, range: 3.5, bullets: 1, knockback: 0.25),
                ToolType.Guard => new Turret("💂", "卫兵", dmg: 5, fireRate: 0.8, range: 4.5, bullets: 2, knockback: 0.45),
                _ => null
            };
            AddFloat(r, c, "放置成功", Color.LightGreen);
        }
        UpdateButtons();
    }

    private void Tip(string msg)
    {
        _lblTip.Text = msg;
        var t = new System.Windows.Forms.Timer { Interval = 1500 };
        t.Tick += (s, e) => { if (_lblTip.Text == msg) _lblTip.Text = ""; t.Stop(); t.Dispose(); };
        t.Start();
    }

    private void AddFloat(int r, int c, string text, Color color, double life = 1.2) =>
        _floats.Add(new FloatText(text, c + 0.5, r, color, life));

    // ---- 游戏主循环 ----
    private void UpdateGame(double dt)
    {
        if (_gameOver) return;
        _elapsed += dt;
        _waveTimer += dt;
        if (_waveTimer >= 25) { _waveTimer = 0; _wave++; }

        // 刷怪：开局8秒准备时间，之后间隔随波数缓慢缩短，最快不低于3秒
        _spawnTimer += dt;
        double interval = Math.Max(3.0, 4.5 - _wave * 0.08);
        if (_elapsed >= 8 && _spawnTimer >= interval)
        {
            _spawnTimer = 0;
            double speed = 0.9 + Math.Min(0.8, _wave * 0.05);
            _monsters.Add(new Monster(SpawnCell.c, SpawnCell.r, speed));
        }

        // 农民生产芝士（每5秒稳定+25）
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_buildings[r, c] is Farmer f)
                {
                    int got = f.Tick(dt);
                    if (got > 0)
                    {
                        _cheese += got;
                        _floats.Add(new FloatText($"+{got} 🧀", c + 0.5, r, Color.Gold, 1.6));
                        UpdateButtons();
                    }
                }

        // 炮台射击
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_buildings[r, c] is Turret t)
                {
                    t.Cooldown -= dt;
                    if (t.Cooldown > 0) continue;
                    double cx = c + 0.5, cy = r + 0.5;
                    var targets = _monsters
                        .Where(m => Dist(m.X, m.Y, cx, cy) <= t.Range)
                        .OrderBy(m => Dist(m.X, m.Y, cx, cy))
                        .ToList();
                    if (targets.Count == 0) continue;
                    t.Cooldown = 1.0 / t.FireRate;
                    for (int i = 0; i < t.BulletsPerShot; i++)
                    {
                        var tgt = targets[i % targets.Count];
                        _bullets.Add(new Bullet(cx, cy, tgt, t.Dmg, t.Knockback));
                    }
                }

        // 子弹
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.Life -= dt;
            if (b.Target != null && b.Target.Hp > 0)
            {
                double dx = b.Target.X - b.X, dy = b.Target.Y - b.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001) { b.Vx = dx / len * Bullet.Speed; b.Vy = dy / len * Bullet.Speed; }
            }
            b.X += b.Vx * dt; b.Y += b.Vy * dt;

            bool hit = b.Target != null && b.Target.Hp > 0 && Dist(b.X, b.Y, b.Target.X, b.Target.Y) < 0.45;
            if (hit && b.Target != null)
            {
                b.Target.Hp -= (int)b.Dmg;
                // 击退：沿子弹飞行方向推动，逐小步移动且不把怪物推进墙
                if (b.Knockback > 0)
                {
                    double klen = Math.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy);
                    if (klen > 0.001)
                    {
                        double kx = b.Vx / klen, ky = b.Vy / klen;
                        double moved = 0;
                        while (moved < b.Knockback)
                        {
                            double nx = b.Target.X + kx * 0.05, ny = b.Target.Y + ky * 0.05;
                            int nr = (int)Math.Round(ny), nc = (int)Math.Round(nx);
                            if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) break;
                            if (_map[nr, nc] == TileType.Wall) break;
                            b.Target.X = nx; b.Target.Y = ny;
                            moved += 0.05;
                        }
                    }
                }
                _floats.Add(new FloatText($"-{(int)b.Dmg}", b.X, b.Y - 0.3, Color.Orange, 0.7));
            }
            if (hit || b.Life <= 0) _bullets.RemoveAt(i);
        }
        _monsters.RemoveAll(m => m.Hp <= 0);

        // 怪物移动 / 破坏路障 / 撞基地
        foreach (var m in _monsters)
        {
            int mr = (int)Math.Round(m.Y), mc = (int)Math.Round(m.X);
            // 距离基地足够近即命中（不依赖圆整格，避免卡在基地门口半径死角）
            if (Dist(m.X, m.Y, BaseCell.c, BaseCell.r) < 0.55)
            {
                m.Hp = 0;
                _playerHp--;
                AddFloat(BaseCell.r, BaseCell.c, "💥 -1❤", Color.Red, 1.5);
                if (_playerHp <= 0) { _gameOver = true; _gameTimer.Stop(); }
                continue;
            }

            // 加权寻路：选成本更低的相邻格（路障格成本高，有绕路则绕行）
            double best = _dist[mr, mc];
            int br = -1, bc = -1;
            int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nr = mr + dr[i], nc = mc + dc[i];
                if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) continue;
                if (double.IsPositiveInfinity(_dist[nr, nc])) continue;
                if (_dist[nr, nc] < best) { best = _dist[nr, nc]; br = nr; bc = nc; }
            }

            if (br == -1)
            {
                // 没有更近的邻居：多半已站在基地格边缘，直接朝基地中心走
                double dx0 = BaseCell.c - m.X, dy0 = BaseCell.r - m.Y;
                double len0 = Math.Sqrt(dx0 * dx0 + dy0 * dy0);
                if (len0 > 0.001) { m.X += dx0 / len0 * m.Speed * dt; m.Y += dy0 / len0 * m.Speed * dt; }
                continue;
            }
            m.Attacking = _stops[br, bc] != null;
            if (m.Attacking)
            {
                // 每秒破坏1点路障耐久
                m.AtkTimer += dt;
                if (m.AtkTimer >= 1.0)
                {
                    m.AtkTimer = 0;
                    var stop = _stops[br, bc]!;
                    stop.Hp--;
                    _floats.Add(new FloatText("🔨", bc + 0.5, br, Color.LightGray, 0.6));
                    if (stop.Hp <= 0)
                    {
                        _stops[br, bc] = null;
                        AddFloat(br, bc, "💨 破坏", Color.Gray, 1.0);
                        ComputeDistField(); // 路障被拆，重新计算加权距离场
                    }
                }
            }
            else
            {
                double dx = bc - m.X, dy = br - m.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001) { m.X += dx / len * m.Speed * dt; m.Y += dy / len * m.Speed * dt; }
            }
        }
        _monsters.RemoveAll(m => m.Hp <= 0);

        // 飘字
        for (int i = _floats.Count - 1; i >= 0; i--)
        {
            var f = _floats[i];
            f.Life -= dt; f.Y -= dt * 0.5;
            if (f.Life <= 0) _floats.RemoveAt(i);
        }

        UpdateLabels();
    }

    private static double Dist(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void UpdateLabels()
    {
        _lblCheese.Text = $"🧀 芝士：{_cheese}";
        _lblHp.Text = $"❤ 血量：{new string('♥', Math.Max(0, _playerHp))}{new string('♡', Math.Max(0, 3 - _playerHp))}";
        _lblWave.Text = $"🌊 第 {_wave} 波  ⏱ {(int)_elapsed}s";
        UpdateButtons();
    }

    // ---- 渲染 ----
    private void Board_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        g.FillRectangle(SceneBrush, _board.ClientRectangle);

        var hover = _board.PointToClient(Cursor.Position);

        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var rect = new Rectangle(c * Cell, r * Cell, Cell, Cell);
                var tile = _map[r, c];
                g.FillRectangle(tile switch
                {
                    TileType.Wall => WallBrush,
                    TileType.Ground => GroundBrush,
                    TileType.Spawn => Brushes.Firebrick,
                    TileType.Base => Brushes.ForestGreen,
                    _ => Brushes.Black
                }, rect);
                g.DrawRectangle(tile == TileType.Wall ? WallPen : GroundPen, rect);

                // 放置预览高亮
                if (_tool != ToolType.None && rect.Contains(hover))
                {
                    bool ok = _tool == ToolType.Stop
                        ? tile == TileType.Ground && _stops[r, c] == null
                        : tile == TileType.Wall && _buildings[r, c] == null;
                    g.DrawRectangle(ok ? OkPen : BadPen, rect);
                }
            }

        // 路障 ✋ + 耐久条
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_stops[r, c] is StopBarrier stop)
                {
                    DrawEmoji(g, "✋", r, c);
                    g.FillRectangle(Brushes.DimGray, c * Cell + 6, r * Cell + Cell - 7, Cell - 12, 4);
                    g.FillRectangle(Brushes.Cyan, c * Cell + 6, r * Cell + Cell - 7,
                        (float)((Cell - 12) * stop.Hp / (double)StopBarrier.MaxHp), 4);
                }

        // 建筑（农民带生产进度条）
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_buildings[r, c] is Building b)
                {
                    DrawEmoji(g, b.Emoji, r, c);
                    if (b is Farmer f)
                    {
                        g.FillRectangle(Brushes.DimGray, c * Cell + 6, r * Cell + Cell - 7, Cell - 12, 4);
                        g.FillRectangle(Brushes.Gold, c * Cell + 6, r * Cell + Cell - 7,
                            (float)((Cell - 12) * Math.Min(1.0, f.Timer / (double)Farmer.Interval)), 4);
                    }
                }

        // 怪物 + 血条
        foreach (var m in _monsters)
        {
            DrawEmojiAt(g, "😈", (float)(m.X * Cell), (float)(m.Y * Cell));
            g.FillRectangle(Brushes.DimGray, (float)(m.X * Cell) + 6, (float)(m.Y * Cell) - 3, Cell - 12, 4);
            g.FillRectangle(Brushes.Red, (float)(m.X * Cell) + 6, (float)(m.Y * Cell) - 3,
                (float)((Cell - 12) * Math.Max(0, m.Hp) / (double)Monster.MaxHp), 4);
        }

        // 子弹
        foreach (var b in _bullets)
            g.FillEllipse(Brushes.Gold, (float)(b.X * Cell) - 4, (float)(b.Y * Cell) - 4, 8, 8);

        // 飘字
        foreach (var f in _floats)
        {
            int alpha = (int)(Math.Max(0, f.Life) / f.MaxLife * 255);
            using var brush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), f.Color));
            g.DrawString(f.Text, SmallFont, brush, (float)(f.X * Cell) - 16, (float)(f.Y * Cell) - 10);
        }

        // 起终点标记
        DrawEmoji(g, "😈", SpawnCell.r, SpawnCell.c);
        DrawEmoji(g, "🏠", BaseCell.r, BaseCell.c);

        // GameOver 遮罩
        if (_gameOver)
        {
            using var overlay = new SolidBrush(Color.FromArgb(165, 0, 0, 0));
            g.FillRectangle(overlay, _board.ClientRectangle);
            g.DrawString("GAME OVER", BigFont, Brushes.IndianRed, _board.Width / 2 - 118, _board.Height / 2 - 48);
            g.DrawString("点击右侧「重新开始」再来一局", SmallFont, Brushes.White, _board.Width / 2 - 82, _board.Height / 2 + 16);
        }
    }

    private static void DrawEmoji(Graphics g, string emoji, int r, int c) =>
        DrawEmojiAt(g, emoji, c * Cell, r * Cell);

    private static void DrawEmojiAt(Graphics g, string emoji, float x, float y)
    {
        var sz = g.MeasureString(emoji, EmojiFont);
        g.DrawString(emoji, EmojiFont, Brushes.White, x + (Cell - sz.Width) / 2, y + (Cell - sz.Height) / 2 + 2);
    }

    // ---- 重开 ----
    private void Restart()
    {
        _cheese = 50; _playerHp = 3; _gameOver = false;
        _elapsed = 0; _wave = 1; _waveTimer = 0; _spawnTimer = 0;
        _monsters.Clear(); _bullets.Clear(); _floats.Clear();
        Array.Clear(_buildings);
        Array.Clear(_stops);
        _tool = ToolType.None;
        UpdateButtons();
        _lblTip.Text = "";
        _lastTime = _clock.Elapsed.TotalSeconds;
        _gameTimer.Start();
    }
}
