using System.Windows;
using System.Windows.Controls;
using Mainframe.ViewModels;

namespace Mainframe.Views;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    // refresh the overview each time the tab is navigated to
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is MainViewModel vm)
            vm.RefreshOverview();
    }
}
