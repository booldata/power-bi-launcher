using Microsoft.UI.Xaml.Controls;

namespace PowerBILauncher;

public sealed partial class ProfileBoardPage : Page
{
    public ProfileBoardPage()
    {
        InitializeComponent();
    }

    public ScrollViewer BoardScroller => ProfileBoardScroller;

    public Grid Board => ProfileBoard;

    public Canvas InsertionLayer => ReorderInsertionLayer;

    public StackPanel EmptyBoard => EmptyBoardPanel;

    public Button EmptyAddButton => EmptyBoardAddButton;
}
