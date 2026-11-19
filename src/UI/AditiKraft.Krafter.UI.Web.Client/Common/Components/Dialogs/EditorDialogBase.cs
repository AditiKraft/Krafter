namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;

public abstract class EditorDialogBase(DialogService dialogService) : ComponentBase, IDisposable
{
    [CascadingParameter] public Dialog? EditorDialog { get; set; }
    private bool showClose;

    protected bool IsSaving { get; private set; }
    protected bool IsDisposed { get; private set; }

    protected override void OnInitialized()
    {
        if (EditorDialog is not null)
        {
            // Radzen installs its Escape handler only on the first render. Route dismissal
            // through this editor's Cancel/Escape handler so it can honor the pending save.
            showClose = EditorDialog.Options.ShowClose;
            EditorDialog.Options.CloseDialogOnEsc = false;
            EditorDialog.Options.CloseDialogOnOverlayClick = false;
        }
    }

    protected bool TryBeginSave()
    {
        if (IsDisposed || IsSaving)
        {
            return false;
        }

        IsSaving = true;
        if (EditorDialog is not null)
        {
            EditorDialog.Options.ShowClose = false;
        }
        return true;
    }

    protected void EndSave()
    {
        if (!IsDisposed)
        {
            IsSaving = false;
            if (EditorDialog is not null)
            {
                EditorDialog.Options.ShowClose = showClose;
            }
        }
    }

    protected void CloseAfterSave()
    {
        // Navigation or an external close can still dispose an editor during its API call.
        // DialogService.Close closes the newest dialog, so a stale continuation must stop.
        if (!IsDisposed)
        {
            dialogService.Close(true);
        }
    }

    protected void Cancel()
    {
        if (!IsDisposed && !IsSaving)
        {
            dialogService.Close(false);
        }
    }

    protected void OnEditorKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            Cancel();
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
