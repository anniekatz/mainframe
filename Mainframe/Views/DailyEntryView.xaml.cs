using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void HoursTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.SelectAll();
    }

    private void HoursTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.IsKeyboardFocusWithin) return;

        tb.Focus();
        tb.SelectAll();
        e.Handled = true;
    }
}
