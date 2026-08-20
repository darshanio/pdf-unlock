using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfUnlock.Models;
using PdfUnlock.Services;

namespace PdfUnlock.ViewModels;

/// <summary>
/// The prompt shown after a run: which folder passwords worked, which did not, and which
/// to remember. Failures are listed but not selectable.
/// </summary>
public sealed partial class SavePasswordsViewModel(PasswordStore store, IEnumerable<PasswordSaveCandidate> candidates)
    : ViewModelBase
{
    public ObservableCollection<PasswordSaveCandidate> Candidates { get; } = new(candidates);

    public PasswordStore Store { get; } = store;

    public string Heading => Candidates.Any(candidate => candidate.CanSave)
        ? "Remember these passwords?"
        : "No passwords to remember";

    public string Explanation =>
        $"Saved passwords are kept in {Store.SecureStoreName}, never in a settings file. " +
        "Files in a folder of the same name will use them next time.";

    [ObservableProperty] private string _resultText = string.Empty;

    /// <summary>Saves the ticked rows, reporting per-folder failures rather than a single
    /// blanket success. A keychain prompt the user dismisses is an ordinary outcome.</summary>
    public void SaveSelected()
    {
        var saved = 0;
        var problems = new List<string>();

        foreach (var candidate in Candidates.Where(candidate => candidate.CanSave && candidate.IsSelected))
        {
            var result = Store.Save(candidate.FolderName, candidate.Path, candidate.Password);
            if (result is SaveRuleResult.Saved or SaveRuleResult.Updated)
            {
                saved++;
                continue;
            }

            problems.Add(result switch
            {
                SaveRuleResult.SecretRejected => $"{candidate.FolderName}: {Store.SecureStoreName} refused to store it",
                SaveRuleResult.NoSecureStore => $"{candidate.FolderName}: no secure store on this system",
                SaveRuleResult.StoreDisabled => $"{candidate.FolderName}: saving is switched off",
                SaveRuleResult.InvalidFolderName => $"{candidate.FolderName}: that folder is too broad to use as a rule",
                _ => $"{candidate.FolderName}: not saved",
            });
        }

        ResultText = problems.Count == 0
            ? $"{saved} saved."
            : $"{saved} saved. " + string.Join("; ", problems);
    }
}
