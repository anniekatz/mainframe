using System.Windows;
using System.Windows.Controls;
using Mainframe.ViewModels;

namespace Mainframe.Views;

public partial class DailyEntryView : UserControl
{
    public DailyEntryView()
    {
        InitializeComponent();
    }

    private void TaskComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.IsDropDownOpen || cb.IsKeyboardFocusWithin) return;
        if (cb.DataContext is not TimeEntryRowViewModel row) return;

        var text = cb.Text?.Trim();
        if (!string.IsNullOrEmpty(text) && cb.SelectedItem == null)
            row.CreateTaskFromText(text);
    }

    private void SubtaskComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.IsDropDownOpen || cb.IsKeyboardFocusWithin) return;
        if (cb.DataContext is not TimeEntryRowViewModel row) return;

        var text = cb.Text?.Trim();
        if (!string.IsNullOrEmpty(text) && cb.SelectedItem == null)
            row.CreateSubtaskFromText(text);
    }
}
