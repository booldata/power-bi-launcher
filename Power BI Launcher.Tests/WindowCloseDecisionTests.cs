namespace PowerBILauncher.Tests;

public class WindowCloseDecisionTests
{
    [Fact]
    public void Decide_allows_close_when_force_closing()
    {
        Assert.Equal(
            WindowCloseAction.AllowClose,
            WindowCloseDecision.Decide(
                WindowClosePhase.ForceClosing,
                WindowCloseIntent.ExplicitExit,
                CloseButtonBehavior.SendToTray,
                editSessionDirty: true));
    }

    [Fact]
    public void Decide_cancels_silently_while_awaiting_dirty_discard()
    {
        Assert.Equal(
            WindowCloseAction.CancelSilently,
            WindowCloseDecision.Decide(
                WindowClosePhase.AwaitingDirtyDiscard,
                WindowCloseIntent.ExplicitExit,
                CloseButtonBehavior.Close,
                editSessionDirty: true));
    }

    [Fact]
    public void Decide_hides_on_title_bar_close_when_send_to_tray_even_if_dirty()
    {
        Assert.Equal(
            WindowCloseAction.CancelAndHide,
            WindowCloseDecision.Decide(
                WindowClosePhase.Idle,
                WindowCloseIntent.TitleBarClose,
                CloseButtonBehavior.SendToTray,
                editSessionDirty: true));
    }

    [Fact]
    public void Decide_prompts_discard_on_explicit_exit_when_dirty()
    {
        Assert.Equal(
            WindowCloseAction.CancelAndPromptDiscard,
            WindowCloseDecision.Decide(
                WindowClosePhase.Idle,
                WindowCloseIntent.ExplicitExit,
                CloseButtonBehavior.SendToTray,
                editSessionDirty: true));
    }

    [Fact]
    public void Decide_allows_close_on_explicit_exit_when_clean()
    {
        Assert.Equal(
            WindowCloseAction.AllowClose,
            WindowCloseDecision.Decide(
                WindowClosePhase.Idle,
                WindowCloseIntent.ExplicitExit,
                CloseButtonBehavior.SendToTray,
                editSessionDirty: false));
    }

    [Fact]
    public void Decide_prompts_discard_on_title_bar_close_when_close_behavior_is_close_and_dirty()
    {
        Assert.Equal(
            WindowCloseAction.CancelAndPromptDiscard,
            WindowCloseDecision.Decide(
                WindowClosePhase.Idle,
                WindowCloseIntent.TitleBarClose,
                CloseButtonBehavior.Close,
                editSessionDirty: true));
    }

    [Fact]
    public void Decide_allows_close_on_title_bar_close_when_close_behavior_is_close_and_clean()
    {
        Assert.Equal(
            WindowCloseAction.AllowClose,
            WindowCloseDecision.Decide(
                WindowClosePhase.Idle,
                WindowCloseIntent.TitleBarClose,
                CloseButtonBehavior.Close,
                editSessionDirty: false));
    }
}
