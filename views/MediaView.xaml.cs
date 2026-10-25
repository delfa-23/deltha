using System.Windows.Controls;
using Deltha.Services;
using Deltha.ViewModels;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace Deltha.Views;

public partial class MediaView : WpfUserControl
{
    public MediaView()
    {
        InitializeComponent();
    }

    public MediaView(MediaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
