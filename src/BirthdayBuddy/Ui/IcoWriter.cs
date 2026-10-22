using System.Text;

namespace BirthdayBuddy.Ui;

/// <summary>Writes square 32bpp bitmaps into a multi-size .ico (classic DIB entries with alpha).</summary>
public static class IcoWriter
{
    public static void Write(Stream output, IReadOnlyList<Bitmap> images)
    {
        var payloads = images.Select(Dib).ToList();
        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

        w.Write((ushort)0);              // reserved
        w.Write((ushort)1);              // type: icon
        w.Write((ushort)images.Count);

        int offset = 6 + 16 * images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            int size = images[i].Width;
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);            // palette colours
            w.Write((byte)0);            // reserved
            w.Write((ushort)1);          // planes
            w.Write((ushort)32);         // bits per pixel
            w.Write(payloads[i].Length);
            w.Write(offset);
            offset += payloads[i].Length;
        }

        foreach (var payload in payloads) w.Write(payload);
    }

    static byte[] Dib(Bitmap bmp)
    {
        int size = bmp.Width;
        int maskStride = (size + 31) / 32 * 4;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(40);                     // BITMAPINFOHEADER size
        w.Write(size);
        w.Write(size * 2);               // XOR + AND masks
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write(0);                      // BI_RGB
        w.Write(size * size * 4 + maskStride * size);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);

        for (int y = size - 1; y >= 0; y--)      // bottom-up BGRA
        for (int x = 0; x < size; x++)
        {
            var c = bmp.GetPixel(x, y);
            w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
        }

        w.Write(new byte[maskStride * size]);    // AND mask unused: alpha carries transparency
        return ms.ToArray();
    }
}
