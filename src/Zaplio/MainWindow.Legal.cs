using System.Windows;

namespace Zaplio;

public partial class MainWindow
{
    /// <summary>Shows the content-responsibility notice until the user has accepted it once. Returns false when declined.</summary>
    private bool ConfirmLegalNotice()
    {
        if (_state.Settings.LegalNoticeAccepted) return true;
        var dialog = new LegalNoticeWindow(requireAcceptance: true) { Owner = this };
        if (dialog.ShowDialog() != true) return false;

        _state.Settings = _state.Settings with { LegalNoticeAccepted = true };
        try { SaveState(); }
        catch (SourceException exception) { SetFooter(exception.Message, isError: true); } // the notice simply shows again next time
        return true;
    }
}
