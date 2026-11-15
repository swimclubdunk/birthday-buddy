namespace BirthdayBuddy.Ui;

/// <summary>Falling, spinning paper confetti for a rectangle. Call <see cref="Step"/> per frame, then <see cref="Draw"/>.</summary>
public sealed class Confetti
{
    static readonly Color[] Colors =
    [
        Color.FromArgb(0xE8, 0x3E, 0x6C), Color.FromArgb(0xF5, 0xB7, 0x00), Color.FromArgb(0x2E, 0xB8, 0xA6),
        Color.FromArgb(0x6C, 0x5C, 0xE7), Color.FromArgb(0x3D, 0x9B, 0xE9), Color.FromArgb(0x7A, 0xC9, 0x43),
    ];

    struct Piece
    {
        public float X, Y, Vx, Vy, Angle, Spin, Size;
        public int Color;
    }

    readonly Piece[] _pieces;
    readonly Random _random;
    Size _area;

    public Confetti(Size area, int count, int seed)
    {
        _area = area;
        _random = new Random(seed);
        _pieces = new Piece[count];
        for (int i = 0; i < count; i++) _pieces[i] = NewPiece(startAnywhere: true);
    }

    public void Resize(Size area) => _area = area;

    /// <param name="seconds">Time since the last step.</param>
    public void Step(float seconds)
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var p = ref _pieces[i];
            p.X += p.Vx * seconds;
            p.Y += p.Vy * seconds;
            p.Angle += p.Spin * seconds;
            if (p.Y > _area.Height + p.Size || p.X < -p.Size || p.X > _area.Width + p.Size) p = NewPiece(startAnywhere: false);
        }
    }

    public void Draw(Graphics g)
    {
        foreach (var p in _pieces)
        {
            var state = g.Save();
            g.TranslateTransform(p.X, p.Y);
            g.RotateTransform(p.Angle);
            // Squash by the spin to fake a paper flip.
            float flip = MathF.Abs(MathF.Cos(p.Angle * MathF.PI / 90f));
            using var brush = new SolidBrush(Colors[p.Color]);
            g.FillRectangle(brush, -p.Size / 2, -p.Size * 0.3f * flip, p.Size, p.Size * 0.6f * flip + 0.5f);
            g.Restore(state);
        }
    }

    Piece NewPiece(bool startAnywhere)
    {
        float unit = Math.Max(1, _area.Height / 135f);
        return new Piece
        {
            X = (float)_random.NextDouble() * _area.Width,
            Y = startAnywhere ? (float)_random.NextDouble() * _area.Height : -8 * unit,
            Vx = ((float)_random.NextDouble() - 0.5f) * 30 * unit,
            Vy = (35 + (float)_random.NextDouble() * 45) * unit,
            Angle = (float)_random.NextDouble() * 360,
            Spin = ((float)_random.NextDouble() - 0.5f) * 540,
            Size = (4 + (float)_random.NextDouble() * 4) * unit,
            Color = _random.Next(Colors.Length),
        };
    }
}
