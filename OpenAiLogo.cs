using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CodexMinder;

internal static class OpenAiLogo
{
    // Render the original filled Blossom path from OpenAI's SVG; omit construction guides.
    public static GraphicsPath Load()
    {
        using var stream = typeof(OpenAiLogo).Assembly.GetManifestResourceStream("CodexMinder.Assets.Blossom_Light.svg")!;
        var svg = XDocument.Load(stream);
        var data = svg.Descendants().First(e => e.Name.LocalName == "path" && (string?)e.Attribute("fill") == "black").Attribute("d")!.Value;
        var tokens = Regex.Matches(data, @"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?").Select(m => m.Value).ToArray();
        var path = new GraphicsPath(FillMode.Winding);
        int i = 0;
        float x = 0, y = 0;
        char command = ' ';
        float Number() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
        while (i < tokens.Length)
        {
            if (char.IsLetter(tokens[i][0])) command = tokens[i++][0];
            switch (command)
            {
                case 'M': path.StartFigure(); x = Number(); y = Number(); command = 'L'; break;
                case 'L': { float nx = Number(), ny = Number(); path.AddLine(x, y, nx, ny); x = nx; y = ny; break; }
                case 'H': { float nx = Number(); path.AddLine(x, y, nx, y); x = nx; break; }
                case 'V': { float ny = Number(); path.AddLine(x, y, x, ny); y = ny; break; }
                case 'C': { float ax = Number(), ay = Number(), bx = Number(), by = Number(), nx = Number(), ny = Number(); path.AddBezier(x, y, ax, ay, bx, by, nx, ny); x = nx; y = ny; break; }
                case 'Z': path.CloseFigure(); command = ' '; break;
                default: throw new InvalidDataException("Unexpected logo path command.");
            }
        }
        return path;
    }

    public static void Draw(Graphics graphics, GraphicsPath path, Rectangle box, Color? color = null)
    {
        var bounds = path.GetBounds();
        var size = Math.Min(box.Width, box.Height);
        var scale = size / Math.Max(bounds.Width, bounds.Height);
        using var transform = new Matrix();
        transform.Translate(box.X + (box.Width - bounds.Width * scale) / 2, box.Y + (box.Height - bounds.Height * scale) / 2);
        transform.Scale(scale, scale);
        transform.Translate(-bounds.X, -bounds.Y);
        using var copy = (GraphicsPath)path.Clone();
        copy.Transform(transform);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color ?? Color.White);
        graphics.FillPath(brush, copy);
    }
}
