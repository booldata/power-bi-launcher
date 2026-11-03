using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerBILauncher;

/// <summary>
/// Equal-width profile card grid (max 5 columns) inside a ScrollViewer host.
/// </summary>
public static class ProfileBoardLayout
{
    public const double CardMinWidth = 240;
    public const double CardGap = 16;
    public const int MaxColumns = 5;
    public const double CardMinHeight = 120;

    /// <summary>
    /// How many equal-width columns fit in <paramref name="availableWidth"/>
    /// (<c>n*CardMinWidth + (n-1)*CardGap &lt;= availableWidth</c>).
    /// </summary>
    public static int ColumnsThatFit(double availableWidth)
    {
        if (availableWidth <= 0)
        {
            return 1;
        }

        return System.Math.Max(
            1,
            (int)System.Math.Floor((availableWidth + CardGap) / (CardMinWidth + CardGap)));
    }

    public static void Apply(Grid board, IReadOnlyList<Button> cards, double availableWidth)
    {
        if (availableWidth <= 0 || board is null)
        {
            return;
        }

        board.Width = availableWidth;

        int count = cards.Count;
        if (count == 0)
        {
            if (board.ColumnDefinitions.Count > 0 || board.RowDefinitions.Count > 0)
            {
                board.ColumnDefinitions.Clear();
                board.RowDefinitions.Clear();
            }

            return;
        }

        int cols = System.Math.Min(MaxColumns, System.Math.Min(count, ColumnsThatFit(availableWidth)));
        int rows = (count + cols - 1) / cols;

        // Avoid ColumnDefinitions.Clear on every resize pixel - that thrash native-crashes WinUI
        // (0xc0000409) when SizeChanged fires in a tight loop.
        bool structureChanged =
            board.ColumnDefinitions.Count != cols ||
            board.RowDefinitions.Count != rows;


        if (structureChanged)
        {
            board.ColumnDefinitions.Clear();
            board.RowDefinitions.Clear();

            for (int c = 0; c < cols; c++)
            {
                board.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }

            for (int r = 0; r < rows; r++)
            {
                board.RowDefinitions.Add(new RowDefinition
                {
                    Height = GridLength.Auto
                });
            }
        }

        for (int i = 0; i < count; i++)
        {
            Button card = cards[i];
            int row = i / cols;
            int col = i % cols;
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);

            double left = col == 0 ? 0 : CardGap / 2;
            double right = col == cols - 1 ? 0 : CardGap / 2;
            double top = row == 0 ? 0 : CardGap / 2;
            double bottom = row == rows - 1 ? 0 : CardGap / 2;
            card.Margin = new Thickness(left, top, right, bottom);
            card.MinHeight = CardMinHeight;
            card.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

    }
}
