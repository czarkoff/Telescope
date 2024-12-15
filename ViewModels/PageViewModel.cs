using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Telescope.ViewModels;

public partial class PageViewModel : ObservableObject
{
    public ObservableCollection<string> Content { get; set; } = [];
}
