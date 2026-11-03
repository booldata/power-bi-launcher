using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;

namespace PowerBILauncher;

public enum WindowCloseIntent
{
    TitleBarClose,
    ExplicitExit,
}

public enum WindowClosePhase
{
    Idle,
    AwaitingDirtyDiscard,
    ForceClosing,
}

public enum WindowCloseAction
{
    AllowClose,
    CancelAndHide,
    CancelAndPromptDiscard,
    CancelSilently,
}

/// <summary>
/// Pure close-path decisions for title-bar close, Send-to-tray hide, explicit Exit, and dirty discard.
/// </summary>
public static class WindowCloseDecision
{
    public static WindowCloseAction Decide(
        WindowClosePhase phase,
        WindowCloseIntent intent,
        CloseButtonBehavior closeBehavior,
        bool editSessionDirty)
    {
        if (phase == WindowClosePhase.ForceClosing)
        {
            return WindowCloseAction.AllowClose;
        }

        if (phase == WindowClosePhase.AwaitingDirtyDiscard)
        {
            return WindowCloseAction.CancelSilently;
        }

        if (intent == WindowCloseIntent.TitleBarClose
            && closeBehavior == CloseButtonBehavior.SendToTray)
        {
            return WindowCloseAction.CancelAndHide;
        }

        if (editSessionDirty)
        {
            return WindowCloseAction.CancelAndPromptDiscard;
        }

        return WindowCloseAction.AllowClose;
    }
}

public sealed class WindowCloseGateHost
{
    public required Func<bool> IsEditSessionDirty { get; init; }

    public required Func<Task<bool>> TryDiscardEditSessionAsync { get; init; }

    public required Action CleanupBeforeClose { get; init; }

    public required Action HideWindow { get; init; }

    public required Action ForceCloseWindow { get; init; }
}

/// <summary>
/// Owns tray hide vs explicit Exit vs dirty discard vs force-close. Queries the Edit Profile session
/// for dirty truth and discard; does not reimplement dirty rules.
/// </summary>
public sealed class WindowCloseGate
{
    private readonly WindowCloseGateHost _host;
    private WindowClosePhase _phase = WindowClosePhase.Idle;
    private WindowCloseIntent _intent = WindowCloseIntent.TitleBarClose;

    public WindowCloseGate(WindowCloseGateHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void RequestExplicitExit()
    {
        _intent = WindowCloseIntent.ExplicitExit;
    }

    public async Task HandleClosingAsync(
        AppWindowClosingEventArgs args,
        CloseButtonBehavior closeBehavior)
    {
        WindowCloseAction action = WindowCloseDecision.Decide(
            _phase,
            _intent,
            closeBehavior,
            _host.IsEditSessionDirty());

        switch (action)
        {
            case WindowCloseAction.AllowClose:
                _host.CleanupBeforeClose();
                _phase = WindowClosePhase.ForceClosing;
                return;

            case WindowCloseAction.CancelAndHide:
                args.Cancel = true;
                _host.HideWindow();
                return;

            case WindowCloseAction.CancelSilently:
                args.Cancel = true;
                return;

            case WindowCloseAction.CancelAndPromptDiscard:
                args.Cancel = true;
                _phase = WindowClosePhase.AwaitingDirtyDiscard;
                try
                {
                    bool left = await _host.TryDiscardEditSessionAsync();
                    if (left)
                    {
                        _host.CleanupBeforeClose();
                        _phase = WindowClosePhase.ForceClosing;
                        _host.ForceCloseWindow();
                    }
                    else
                    {
                        _intent = WindowCloseIntent.TitleBarClose;
                    }
                }
                finally
                {
                    if (_phase == WindowClosePhase.AwaitingDirtyDiscard)
                    {
                        _phase = WindowClosePhase.Idle;
                    }
                }

                return;

            default:
                throw new InvalidOperationException($"Unexpected close action: {action}");
        }
    }
}
