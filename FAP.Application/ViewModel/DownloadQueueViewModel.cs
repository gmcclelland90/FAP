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

using System.Collections;
using System.Windows.Input;
using FAP.Application.Views;
using FAP.Domain.Entities;
using Fap.Foundation;

namespace FAP.Application.ViewModels
{
    public class DownloadQueueViewModel : ViewModelBase<IDownloadQueue>
    {
        private ICommand clearDownloadLog = null!;
        private ICommand clearUploadLog = null!;
        private SafeObservingCollection<TransferLog> completedDownloads = null!;
        private SafeObservingCollection<TransferLog> completedUploads = null!;
        private SafeObservingCollection<DownloadRequest> downloadQueue = null!;
        private string downloadStats = string.Empty;

        private ICommand movedown = null!;
        private ICommand movetobottom = null!;
        private ICommand movetotop = null!;
        private ICommand moveup = null!;
        private ICommand removeAll = null!;
        private ICommand removeSelection = null!;
        private IList selectedItems = null!;
        private string uploadStats = string.Empty;

        public DownloadQueueViewModel(IDownloadQueue view)
            : base(view)
        {
        }

        public string DownloadStats
        {
            get { return downloadStats; }
            set
            {
                downloadStats = value;
                OnPropertyChanged("DownloadStats");
            }
        }

        public string UploadStats
        {
            get { return uploadStats; }
            set
            {
                uploadStats = value;
                OnPropertyChanged("UploadStats");
            }
        }

        public ICommand ClearDownloadLog
        {
            get { return clearDownloadLog; }
            set
            {
                clearDownloadLog = value;
                OnPropertyChanged("ClearDownloadLog");
            }
        }

        public ICommand ClearUploadLog
        {
            get { return clearUploadLog; }
            set
            {
                clearUploadLog = value;
                OnPropertyChanged("ClearUploadLog");
            }
        }

        public ICommand Movetobottom
        {
            get { return movetobottom; }
            set
            {
                movetobottom = value;
                OnPropertyChanged("Movetobottom");
            }
        }

        public ICommand Movedown
        {
            get { return movedown; }
            set
            {
                movedown = value;
                OnPropertyChanged("Movedown");
            }
        }

        public ICommand Moveup
        {
            get { return moveup; }
            set
            {
                moveup = value;
                OnPropertyChanged("Moveup");
            }
        }

        public ICommand Movetotop
        {
            get { return movetotop; }
            set
            {
                movetotop = value;
                OnPropertyChanged("Movetotop");
            }
        }

        public ICommand RemoveSelection
        {
            get { return removeSelection; }
            set
            {
                removeSelection = value;
                OnPropertyChanged("RemoveSelection");
            }
        }

        public ICommand RemoveAll
        {
            get { return removeAll; }
            set
            {
                removeAll = value;
                OnPropertyChanged("RemoveAll");
            }
        }

        public SafeObservingCollection<DownloadRequest> DownloadQueue
        {
            set
            {
                downloadQueue = value;
                OnPropertyChanged("DownloadQueue");
            }
            get { return downloadQueue; }
        }

        public SafeObservingCollection<TransferLog> CompletedDownloads
        {
            set
            {
                completedDownloads = value;
                OnPropertyChanged("CompletedDownloads");
            }
            get { return completedDownloads; }
        }

        public SafeObservingCollection<TransferLog> CompletedUploads
        {
            set
            {
                completedUploads = value;
                OnPropertyChanged("CompletedUploads");
            }
            get { return completedUploads; }
        }

        public IList SelectedItems
        {
            set
            {
                selectedItems = value;
                OnPropertyChanged("SelectedItems");
            }
            get { return selectedItems; }
        }
    }
}