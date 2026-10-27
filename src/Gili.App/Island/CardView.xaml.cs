using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Gili.App.Controls;

namespace Gili.App.Island;

public partial class CardView : UserControl
{
    public CardView() => InitializeComponent();

    /// <summary>
    /// The icon's pulse runs only while a card shows, and not when Windows animations are off (mockup .cic.pulse:
    /// scale 1 → 1.65 and opacity 0.8 → 0 over 1.2 s, repeating).
    /// </summary>
    public void SetPulsing(bool pulsing)
    {
        if (!pulsing || !SystemParameters.ClientAreaAnimation)
        {
            PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            PulseRing.BeginAnimation(OpacityProperty, null);
            PulseRing.Opacity = 0;
            return;
        }
        var duration = TimeSpan.FromMilliseconds(1200);
        var grow = new DoubleAnimation(1, 1.65, duration) { EasingFunction = BezierEase.Out, RepeatBehavior = RepeatBehavior.Forever };
        PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, grow);
        PulseScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, grow);
        PulseRing.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8, 0, duration) { EasingFunction = BezierEase.Out, RepeatBehavior = RepeatBehavior.Forever });
    }
}
