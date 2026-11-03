namespace PowerBILauncher;

/// <summary>
/// UI-free main-window minimum size and first-open default width (DIP)
/// from Profile board layout + chrome/padding constants.
/// Page gutter and form max width must match the XAML resources in <c>App.xaml</c>.
/// </summary>
public static class WindowLayoutConstraints
{
    /// <summary>Shared left/right page gutter (4-epx grid). Board, Settings, and Edit Profile start here.</summary>
    public const double PageGutter = 32;

    /// <summary>
    /// Max width of Settings and Edit Profile form columns.
    /// The page ScrollViewer reaches the window edge; only the inner column uses this cap.
    /// </summary>
    public const double FormPageMaxWidth = 800;

    /// <summary>Body content <c>Border</c> left padding.</summary>
    public const double BodyPaddingLeft = PageGutter;

    /// <summary>
    /// Body content <c>Border</c> right padding. Zero so the page <c>ScrollViewer</c> reaches
    /// the window edge (Windows Settings). The visual right gutter is <see cref="BodyPaddingRight"/>.
    /// </summary>
    public const double BodyBorderPaddingRight = 0;

    /// <summary>
    /// Visual right gutter reserved inside each page scroller so the overlay thumb does not cover content.
    /// </summary>
    public const double BodyPaddingRight = PageGutter;

    /// <summary>Body content <c>Border</c> top padding.</summary>
    public const double BodyPaddingTop = 16;

    /// <summary>Body content <c>Border</c> bottom padding.</summary>
    public const double BodyPaddingBottom = 20;

    public const double BodyHorizontalPadding = BodyPaddingLeft + BodyPaddingRight;

    /// <summary>
    /// Settings / Edit Profile form plus left gutter and visual right gutter (800 + 32 + 32).
    /// Does not raise <see cref="MinWidth"/> or set first-open width.
    /// </summary>
    public const double FormFitWidth = FormPageMaxWidth + BodyHorizontalPadding;

    /// <summary>
    /// Max board content width that still fits exactly two columns
    /// (<c>3*CardMinWidth + 2*CardGap - 1</c>).
    /// </summary>
    public static double TwoColumnMaxContentWidth =>
        (3 * ProfileBoardLayout.CardMinWidth) + (2 * ProfileBoardLayout.CardGap) - 1;

    /// <summary>
    /// Initial AppWindow width in DIP: widest window that still shows two profile columns.
    /// Does not raise <see cref="MinWidth"/>.
    /// </summary>
    public static double DefaultWidth =>
        System.Math.Max(MinWidth, TwoColumnMaxContentWidth + BodyHorizontalPadding);

    /// <summary>Top shell row padding above the NavigationView.</summary>
    public const double ShellNavRowPaddingTop = 12;

    /// <summary>Approximate height of the top NavigationView pivot row.</summary>
    public const double ShellNavHeight = 48;

    /// <summary>
    /// Profiles page title row height (TitleTextBlockStyle). Header buttons and the
    /// Reorder mode banner match this so entering the mode does not shift the board.
    /// </summary>
    public const double ProfilesTitleRowHeight = 32;

    /// <summary>Gap between the Profiles title/commanding row and the board Frame.</summary>
    public const double ProfilesTitleToBoardGap = 24;

    /// <summary>
    /// Minimum window width: single-column board breakpoint (495 DIP scroller + page gutters).
    /// </summary>
    public static double MinWidth =>
        SingleColumnMaxScrollerWidth + BodyHorizontalPadding;

    /// <summary>
    /// Card-grid width inside a full-bleed board scroller: scroller width minus the overlay-thumb gutter.
    /// </summary>
    public static double BoardContentWidth(double scrollerWidth) =>
        System.Math.Max(0, scrollerWidth - BodyPaddingRight);

    /// <summary>
    /// Max scroller width that still fits exactly one column
    /// (<c>2*CardMinWidth + CardGap - 1</c>).
    /// </summary>
    public static double SingleColumnMaxScrollerWidth =>
        (2 * ProfileBoardLayout.CardMinWidth) + ProfileBoardLayout.CardGap - 1;

    /// <summary>
    /// Minimum window height for Profiles: nav + title + one card row + padding.
    /// </summary>
    public static double MinHeight =>
        ShellNavRowPaddingTop
        + ShellNavHeight
        + BodyPaddingTop
        + ProfilesTitleRowHeight
        + ProfilesTitleToBoardGap
        + ProfileBoardLayout.CardMinHeight
        + BodyPaddingBottom;
}
