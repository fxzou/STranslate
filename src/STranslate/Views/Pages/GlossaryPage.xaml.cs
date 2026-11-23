using STranslate.ViewModels.Pages;

namespace STranslate.Views.Pages;

public partial class GlossaryPage
{
    public GlossaryPage(GlossaryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();
    }

    public GlossaryViewModel ViewModel { get; }
}
