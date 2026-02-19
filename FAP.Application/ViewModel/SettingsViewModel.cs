#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */

#endregion

using System.Reflection;
using System.Windows.Input;
using FAP.Application.Views;
using FAP.Domain.Entities;
using Fap.Foundation;
using Microsoft.Win32;
using System.ComponentModel;

namespace FAP.Application.ViewModels
{
    public class SettingsViewModel : ViewModelBase<ISettingsView>, IDataErrorInfo
    {
        private readonly string startupRegistryPath = "SOFTWARE/Microsoft/Windows/CurrentVersion/Run";
        private ICommand changeAvatar = null!;
        private ICommand displayQuickStart = null!;
        private ICommand editDownloadDir = null!;
        private Model model = null!;
        private ICommand resetInterface = null!;
        private ICommand saveCommand = null!;
        private ICommand cancelCommand = null!;

        public SettingsViewModel(ISettingsView view)
            : base(view)
        {
        }

        public ICommand ResetInterface
        {
            get { return resetInterface; }
            set
            {
                resetInterface = value;
                OnPropertyChanged("ResetInterface");
            }
        }

        public ICommand EditDownloadDir
        {
            get { return editDownloadDir; }
            set
            {
                editDownloadDir = value;
                OnPropertyChanged("EditDownloadDir");
            }
        }

        public ICommand DisplayQuickStart
        {
            get { return displayQuickStart; }
            set
            {
                displayQuickStart = value;
                OnPropertyChanged("DisplayQuickStart");
            }
        }

        public ICommand ChangeAvatar
        {
            get { return changeAvatar; }
            set
            {
                changeAvatar = value;
                OnPropertyChanged("ChangeAvatar");
            }
        }

        public ICommand SaveCommand
        {
            get { return saveCommand; }
            set
            {
                saveCommand = value;
                OnPropertyChanged("SaveCommand");
            }
        }

        public ICommand CancelCommand
        {
            get { return cancelCommand; }
            set
            {
                cancelCommand = value;
                OnPropertyChanged("CancelCommand");
            }
        }

        public Model Model
        {
            get { return model; }
            set
            {
                model = value;
                OnPropertyChanged("Model");
            }
        }

        public bool RunOnStartUp
        {
            set
            {
                if (value)
                    RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, "FAP", GetStartupCommand());
                else
                    RegistryHelper.SetRegistryData(Registry.CurrentUser, startupRegistryPath, "FAP", string.Empty);
                OnPropertyChanged("RunOnStartUp");
            }
            get
            {
                return (RegistryHelper.GetRegistryData(Registry.CurrentUser, startupRegistryPath + "/FAP") ==
                        GetStartupCommand());
            }
        }

        private string GetStartupCommand()
        {
            string location = Assembly.GetEntryAssembly()!.Location;
            return string.Format("\"{0}\" STARTUP", location);
        }

        public string Error
        {
            get { return this[null!]; }
        }

        public string this[string columnName]
        {
            get { return model[columnName] ?? string.Empty; }
        }
    }
}