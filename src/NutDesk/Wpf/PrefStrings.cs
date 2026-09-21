using System;
using System.ComponentModel;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The preferences window's captions, tooltips and combo entries, read out of the WinForms dialog's
    /// resource set.
    /// </summary>
    /// <remarks>
    /// Pref_Gui.resx and its seven satellites are the only place these strings exist in German, French,
    /// Italian, Russian, Ukrainian and both Chinese scripts. Declaring them again as new keys for the WPF
    /// window would leave six of those languages in English until somebody re-translated every one, so the new
    /// window borrows the old form's resources instead - the wording was never the problem, the look was.
    /// That is also why Pref_Gui itself has to stay in the project after nothing opens it any more.
    /// Nothing here may throw: a missing string costs a label its translation, while an exception would cost
    /// the user the whole preferences window.
    /// </remarks>
    internal static class PrefStrings
    {
        private static readonly ComponentResourceManager Manager = CreateManager();

        /// <summary>The caption of a label, check box, tab or button. Falls back to the control name.</summary>
        internal static string Text(string controlName)
        {
            return Lookup(controlName + ".Text") ?? controlName;
        }

        /// <summary>The hover help of a control, or null so the control simply has no tooltip.</summary>
        internal static string Tip(string controlName)
        {
            return Lookup(controlName + ".ToolTip");
        }

        /// <summary>
        /// A combo box's entries in their original order. The designer names them "Items", "Items1",
        /// "Items2"... and the order matters: most of these combos persist SelectedIndex, not the text.
        /// </summary>
        internal static string[] Items(string controlName, int count)
        {
            var items = new string[count];

            for (var i = 0; i < count; i++)
            {
                var key = controlName + ".Items" + (i == 0 ? string.Empty : i.ToString());
                // Keep the slot even when the lookup fails, so the indexes still line up with the settings.
                items[i] = Lookup(key) ?? string.Empty;
            }

            return items;
        }

        /// <summary>The dialog's own caption.</summary>
        internal static string Title => Lookup("$this.Text") ?? "NutDesk";

        private static string Lookup(string key)
        {
            if (Manager == null)
            {
                return null;
            }

            try
            {
                return Manager.GetString(key);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ComponentResourceManager CreateManager()
        {
            try
            {
                return new ComponentResourceManager(typeof(Pref_Gui));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
