using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telescope.Gemini;

namespace Telescope.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public string? Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public ObservableCollection<GeminiLine> Content { get; set; } = [];

    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string PromptLabel
    {
        get => _promptLabel;
        set => SetProperty(ref _promptLabel, value);
    }

    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    public bool PromptRequested
    {
        get => _promptRequested;
        set => SetProperty(ref _promptRequested, value);
    }

    private bool _urlIsOk => Uri.TryCreate(Url, UriKind.Absolute, out _);

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(_urlIsOk))]
    public async Task NavigateToUrl()
    {
        Content.Clear();

        if (PromptRequested)
            Url = string.Join("?", Url.Split(['?'], 2)[0], Uri.EscapeDataString(PromptText));

        PromptRequested = false;

        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri))
            return;

        try
        {
            switch (await GeminiClient.Request(uri))
            {
                case GeminiSuccessResponse success:
                    if (success.MimeType.MediaType.Equals("text/gemini", StringComparison.OrdinalIgnoreCase))
                        success.Lines!.ForEach(Content.Add);
                    Status = string.Format("Loaded {0}", uri);
                    break;
                case GeminiRedirectResponse redirect:
                    Status = string.Format("Redirected to {0}", redirect.RedirectUrl);
                    Url = redirect.RedirectUrl.ToString();
                    await NavigateToUrl();
                    return;
                case GeminiPromptResponse prompt:
                    PromptRequested = true;
                    PromptLabel = prompt.Prompt;
                    break;
                case GeminiAuthResponse auth:
                    Status = string.Format("Authentication required,  but not supported yet");
                    break;
                case GeminiErrorResponse error:
                    Status = string.Format("Error {0}: {1}", error.StatusCode, error.StatusDetail ?? "unknown error");
                    break;
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    public void CancelPrompt() => PromptRequested = false;

    private string? _url, _status;
    private string _promptLabel = string.Empty, _promptText = string.Empty;
    private bool _promptRequested;
}
