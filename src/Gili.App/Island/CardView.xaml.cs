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
    /// scale 1 → 1.65 and opacity 0.8 → 0 over 1.2 s, repeating). An alarm's icon also shakes (mockup .cic.shake:
    /// 0 → −14° → 12° → −8° → 6° → 0 over 0.7 s, repeating).
    /// </summary>
    public void SetPulsing(bool pulsing, bool shake)
    {
        if (pulsing && shake && SystemParameters.ClientAreaAnimation)
        {
            var turn = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), RepeatBehavior = RepeatBehavior.Forever };
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            foreach (var (at, angle) in new[] { (0.2, -14.0), (0.4, 12.0), (0.6, -8.0), (0.8, 6.0), (1.0, 0.0) })
                turn.KeyFrames.Add(new EasingDoubleKeyFrame(angle, KeyTime.FromPercent(at), ease));
            IconTurn.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, turn);
        }
        else
        {
            IconTurn.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        }
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
