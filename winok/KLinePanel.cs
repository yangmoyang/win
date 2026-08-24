using System.Windows.Forms;

public class KLinePanel : Panel
{
    public KLinePanel()
    {
        // 关键：开启双缓冲
        this.DoubleBuffered = true;

        // 禁用背景清除，避免闪烁
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer |
                      ControlStyles.ResizeRedraw, true);

        this.UpdateStyles();
    }
}
