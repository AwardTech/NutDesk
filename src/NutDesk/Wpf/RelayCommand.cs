using System;
using System.Windows.Input;

namespace NutDesk.Wpf
{
    /// <summary>
    /// The usual delegate-backed ICommand, so the view can bind buttons to plain methods on the shell.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action executeAction;
        private readonly Func<bool> canExecuteCheck;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            executeAction = execute;
            canExecuteCheck = canExecute;
        }

        /// <summary>
        /// Handed straight to the CommandManager: it already re-queries every command when the UI state
        /// changes, so a canExecute check works the moment it is passed in, with nothing to remember to call.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return canExecuteCheck == null || canExecuteCheck();
        }

        public void Execute(object parameter)
        {
            executeAction();
        }
    }
}
