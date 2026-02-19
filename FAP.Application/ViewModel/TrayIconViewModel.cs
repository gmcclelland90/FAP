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

using System.Windows.Input;
using FAP.Application.Views;
using FAP.Domain.Entities;

namespace FAP.Application.ViewModels
{
    public class TrayIconViewModel : ViewModelBase<ITrayIconView>
    {
        private ICommand compare = null!;
        private ICommand exit = null!;
        private ICommand open = null!;
        private ICommand openExternal = null!;
        private ICommand queue = null!;
        private ICommand settings = null!;
        private ICommand shares = null!;
        private ICommand viewshare = null!;


        public TrayIconViewModel(ITrayIconView view)
            : base(view)
        {
        }

        public Model Model { set; get; } = null!;

        public bool ShowIcon
        {
            get { return ViewCore.ShowIcon; }
            set { ViewCore.ShowIcon = value; }
        }

        public ICommand OpenExternal
        {
            set
            {
                openExternal = value;
                OnPropertyChanged("OpenExternal");
            }
            get { return openExternal; }
        }

        public ICommand Compare
        {
            set
            {
                compare = value;
                OnPropertyChanged("Compare");
            }
            get { return compare; }
        }

        public ICommand ViewShare
        {
            set
            {
                viewshare = value;
                OnPropertyChanged("ViewShare");
            }
            get { return viewshare; }
        }

        public ICommand Queue
        {
            set
            {
                queue = value;
                OnPropertyChanged("Queue");
            }
            get { return queue; }
        }

        public ICommand Settings
        {
            set
            {
                settings = value;
                OnPropertyChanged("Settings");
            }
            get { return settings; }
        }

        public ICommand Shares
        {
            set
            {
                shares = value;
                OnPropertyChanged("Shares");
            }
            get { return shares; }
        }


        public ICommand Open
        {
            set
            {
                open = value;
                OnPropertyChanged("Open");
            }
            get { return open; }
        }

        public ICommand Exit
        {
            set
            {
                exit = value;
                OnPropertyChanged("Exit");
            }
            get { return exit; }
        }

        public void Dispose()
        {
            ViewCore.Dispose();
        }
    }
}