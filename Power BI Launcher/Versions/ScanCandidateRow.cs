using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace PowerBILauncher;

public sealed class ScanCandidateRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public ScanCandidateRow(ScanAcceptedVersion accepted, bool isSelected)
    {
        Path = accepted.Path;
        DisplayName = accepted.DisplayName;
        Kind = accepted.Kind;
        IsSupported = !accepted.IsMicrosoftStore;
        string scanNameHead = PbiVersionKindPolicy.ScanNameHead(accepted.Kind);
        ShowKindLabel = !DisplayName.StartsWith(scanNameHead, StringComparison.OrdinalIgnoreCase);
        _isSelected = isSelected && IsSupported;
    }

    public string Path { get; }
    public string DisplayName { get; }
    public PbiVersionKind Kind { get; }
    public bool IsSupported { get; }
    public double RowOpacity => IsSupported ? 1.0 : 0.5;
    public Visibility NotSupportedVisibility => IsSupported ? Visibility.Collapsed : Visibility.Visible;
    public bool ShowKindLabel { get; }
    public Visibility KindLabelVisibility => ShowKindLabel ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            bool next = value && IsSupported;
            if (_isSelected == next)
            {
                return;
            }

            _isSelected = next;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
