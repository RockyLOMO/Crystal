using System.Runtime.CompilerServices;
using Client.MirGraphics;

namespace Client.MirControls;

/// <summary>Optional complete gate frames, driven by the existing LoginSuccess animation and completion event.</summary>
internal static class Classic176GateTransition
{
    internal const int FirstFrame = 3, FrameCount = 10, PlaybackSteps = 12, FrameDelay = 120;
    private sealed class State { internal bool Started, HasFrames; }
    private static readonly ConditionalWeakTable<MirAnimatedControl, State> States = new();

    internal static void Attach(MirAnimatedControl timeline)
    {
        if (States.TryGetValue(timeline, out _)) return;
        var state = new State();
        try
        {
            state.HasFrames = Enumerable.Range(FirstFrame, FrameCount).All(i => Libraries.Classic176FrontEnd.GetSize(i) == new Size(800, 600));
        }
        catch { state.HasFrames = false; }
        States.Add(timeline, state);
    }

    internal static bool TryBegin(MirAnimatedControl timeline)
    {
        if (timeline == null || timeline.IsDisposed) return false;
        if (!States.TryGetValue(timeline, out var state)) return !timeline.Animated;
        if (state.Started) return false;
        state.Started = true;
        timeline.OffSet = 0;
        timeline.AnimationCount = PlaybackSteps;
        timeline.AnimationDelay = timeline.FadeInDelay = FrameDelay;
        // Three-entry atlases use the same finite timeline with the static closed gate.
        return true;
    }

    internal static int Frame(MirAnimatedControl timeline)
    {
        if (timeline == null || !States.TryGetValue(timeline, out var state) || !state.Started || !state.HasFrames) return 0;
        return FirstFrame + Math.Clamp(timeline.Index, 0, FrameCount - 1);
    }
}
