using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace AionInstructPreview.Chat.Controls;

// Three dots that pulse in sequence ("AI is thinking" indicator).
// Storyboard is constructed in code so it can be wired to the named
// Ellipse elements that the XAML emits, then started/stopped on
// Loaded/Unloaded so the animation never leaks past the visual tree.
public sealed partial class TypingIndicator : UserControl
{
    private Storyboard? _storyboard;

    public TypingIndicator()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _storyboard = new Storyboard();
        AddPulse(_storyboard, Dot1, beginSeconds: 0.0);
        AddPulse(_storyboard, Dot2, beginSeconds: 0.2);
        AddPulse(_storyboard, Dot3, beginSeconds: 0.4);
        _storyboard.Begin();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _storyboard?.Stop();
        _storyboard = null;
    }

    private static void AddPulse(Storyboard sb, UIElement target, double beginSeconds)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(1.2),
            BeginTime = TimeSpan.FromSeconds(beginSeconds),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
            Value = 0.3,
        });
        anim.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)),
            Value = 1.0,
        });
        anim.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6)),
            Value = 0.3,
        });

        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
    }
}
