using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PowerBILauncher;

/// <summary>
/// Per-card board state for Launch / Reorder mode, overflow More, and Launch Order chrome.
/// </summary>
public sealed class ProfileCardModel
{
    public required Profile Profile { get; init; }

    /// <summary>
    /// Reorder mode that drives this card's activate policy and chrome.
    /// </summary>
    public bool BoardEditMode { get; set; }

    /// <summary>
    /// Per-card More (`...`) opener for the overflow MenuFlyout.
    /// </summary>
    public Button? MoreButton { get; set; }

    /// <summary>
    /// Leading-edge grab handle for Launch Order drag (Reorder mode only).
    /// </summary>
    public FrameworkElement? GrabHandle { get; set; }

    /// <summary>
    /// Resting card border brush (theme stroke at create time).
    /// </summary>
    public Brush? IdleBorderBrush { get; set; }

    /// <summary>
    /// True while a Launch Order drag is in progress from this card.
    /// </summary>
    public bool IsLaunchOrderDragging { get; set; }

    /// <summary>
    /// True while the pointer is over this card (drives adaptive More with focus / flyout latch).
    /// </summary>
    public bool IsPointerOver { get; set; }

    /// <summary>
    /// Last pointer interaction on this card was touch or pen (keeps More revealed
    /// until a mouse leave clears it). Not derived from digitizer capability.
    /// </summary>
    public bool TouchOrPenInteraction { get; set; }

    /// <summary>
    /// True while More MenuFlyout or card ContextFlyout is open (keeps More visible).
    /// </summary>
    public bool IsOverflowFlyoutOpen { get; set; }

    /// <summary>
    /// Card or More currently has keyboard focus.
    /// </summary>
    public bool HasKeyboardFocusWithin { get; set; }

    /// <summary>
    /// True while this card is in the transient Launching state.
    /// </summary>
    public bool IsLaunching { get; set; }

    public FontIcon? PlayIcon { get; set; }

    public ProgressRing? LaunchProgress { get; set; }

    public TextBlock? CaptionBlock { get; set; }

    public TextBlock? LaunchingLabel { get; set; }

    public Grid? ProductIconHost { get; set; }

    public FontIcon? IdentityIcon { get; set; }
}
