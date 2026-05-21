using System.Windows.Controls;
using System.Windows.Input;

namespace XboxMetroLauncher.Views.Tabs;

public partial class BingTabView : UserControl
{
    public BingTabView()
    {
        InitializeComponent();
    }

    private void SearchBoxFrame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!BingSearchBox.IsKeyboardFocusWithin)
        {
            BingSearchBox.Focus();
            BingSearchBox.CaretIndex = BingSearchBox.Text.Length;
            e.Handled = true;
        }
    }

    private void BingSearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is ViewModels.Tabs.BingTabViewModel viewModel &&
            viewModel.SubmitSearchCommand.CanExecute(null))
        {
            viewModel.SubmitSearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
