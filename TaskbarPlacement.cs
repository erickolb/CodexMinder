using System.Windows.Automation;

namespace CodexMinder;

internal static class TaskbarPlacement
{
    public static Point? FindGap(IntPtr taskbar, Rectangle bar, Size widget)
    {
        if (bar.Width < bar.Height) return null;
        try
        {
            var occupied = new List<(int Start, int End)>();
            var root = AutomationElement.FromHandle(taskbar);
            var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)));
            foreach (AutomationElement element in controls)
            {
                var r = element.Current.BoundingRectangle;
                if (element.Current.IsOffscreen || r.IsEmpty || r.Width <= 0 || r.Height <= 0 || r.Width >= bar.Width * .9) continue;
                if (r.Bottom <= bar.Top || r.Top >= bar.Bottom) continue;
                occupied.Add(((int)r.Left - 8, (int)r.Right + 8));
            }
            // If accessibility is unavailable, don't guess and cover someone's buttons.
            if (occupied.Count == 0) return null;
            int cursor = bar.Left + 12;
            var gaps = new List<(int Start, int End)>();
            foreach (var interval in occupied.OrderBy(p => p.Start))
            {
                if (interval.Start > cursor) gaps.Add((cursor, interval.Start));
                cursor = Math.Max(cursor, interval.End);
            }
            if (cursor < bar.Right - 12) gaps.Add((cursor, bar.Right - 12));
            var gap = gaps.Where(g => g.End - g.Start >= widget.Width + 16).OrderByDescending(g => g.Start).FirstOrDefault();
            return gap == default ? null : new Point(gap.End - widget.Width - 8, bar.Top + Math.Max(0, (bar.Height - widget.Height) / 2));
        }
        catch (Exception) { return null; }
    }
}
