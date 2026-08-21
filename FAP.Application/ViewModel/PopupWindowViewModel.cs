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

using System.Collections.ObjectModel;
using System.Windows.Input;
using FAP.Application.Views;

namespace FAP.Application.ViewModels
{
    public class PopupWindowViewModel : ViewModelBase<IPopupWindow>
    {
        private readonly ObservableCollection<PopUpWindowTab> documentViews;
        private object activeDocumentView = null!;
        private ICommand close = null!;
        private ICommand tabClose = null!;


        public PopupWindowViewModel(IPopupWindow view)
            : base(view)
        {
            documentViews = new ObservableCollection<PopUpWindowTab>();
        }

        public ObservableCollection<PopUpWindowTab> DocumentViews
        {
            get { return documentViews; }
        }

        public object ActiveDocumentView
        {
            get { return activeDocumentView; }
            set
            {
                activeDocumentView = value;
                OnPropertyChanged("ActiveDocumentView");
            }
        }

        public ICommand TabClose
        {
            get { return tabClose; }
            set
            {
                tabClose = value;
                OnPropertyChanged("TabClose");
            }
        }

        public ICommand Close
        {
            get { return close; }
            set
            {
                close = value;
                OnPropertyChanged("Close");
            }
        }

        public void Show()
        {
            ViewCore.Show();
        }

        public void CloseWindow()
        {
            ViewCore.Close();
        }

        public void FlashIfNotActive()
        {
            ViewCore.FlashIfNotActive();
        }
    }
}