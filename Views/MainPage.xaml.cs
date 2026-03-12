using System;
using final_archiver.ViewModels;
using Microsoft.Maui.Controls;

namespace final_archiver.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        #if MACCATALYST || IOS
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.UserAppTheme = AppTheme.Dark;
        }
        #endif
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is MainViewModel viewModel)
        {
            viewModel.ValidateInputFileCommand.Execute(null);
        }
    }
}