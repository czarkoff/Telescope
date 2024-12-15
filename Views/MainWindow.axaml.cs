using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Telescope.Gemini;
using Telescope.ViewModels;

namespace Telescope.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public async void OnLinkedPressed(object sender, PointerPressedEventArgs args)
        {
            if (DataContext is MainWindowViewModel vm && sender is Control ctl && ctl.DataContext is GeminiLink link)
            {
                if (link.AbsoluteUrl.Scheme == "gemini")
                {
                    vm.Url = link.AbsoluteUrl.ToString();
                    await vm.NavigateToUrl();
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = link.AbsoluteUrl.ToString(),
                        UseShellExecute = true
                    })!.Dispose();
                }
            }
        }
    }
}