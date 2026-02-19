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
using System.Windows.Threading;
using FAP.Application.Views;
using FAP.Domain;
using FAP.Domain.Entities;
using Fap.Foundation;

namespace FAP.Application.ViewModels
{
    public class MainWindowViewModel : ViewModelBase<IMainWindow>
    {
        private bool allowClose;
        private string avatar = string.Empty;
        private ICommand chat = null!;
        private SafeObservingCollection<string> chatList = null!;
        private ICommand closing = null!;
        private ICommand compare = null!;
        private string currentChatMessage = string.Empty;
        private string description = string.Empty;
        private ICommand editShares = null!;
        private string globalStats = string.Empty;
        private string localStats = string.Empty;
        private Model model = null!;
        private string networkInfo = string.Empty;
        private string networkStats = string.Empty;
        private string nickname = string.Empty;
        private Node node = null!;
        private string nodeStatus = string.Empty;
        private ICommand openExternal = null!;
        private SafeFilteredObservingCollection<Node> peers = null!;
        private ICommand search = null!;
        private object selectedClient = null!;
        private ICommand sendChatMessage = null!;
        private SafeObservingCollection<TransferSession> sessions = null!;
        private ICommand settings = null!;
        private PeerSortType sortType;
        private ICommand userinfo = null!;
        private ICommand viewQueue = null!;
        private ICommand viewShare = null!;
        private bool visible;
        private string windowTitle = string.Empty;

        public MainWindowViewModel(IMainWindow view)
            : base(view)
        {
        }

        public PeerSortType PeerSortType
        {
            get { return sortType; }
            set
            {
                sortType = value;
                OnPropertyChanged("PeerSortType");
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

        public string NodeStatus
        {
            get { return nodeStatus; }
            set
            {
                if (nodeStatus != value)
                {
                    nodeStatus = value;
                    OnPropertyChanged("NodeStatus");
                }
            }
        }


        public string CurrentNetworkStatus
        {
            get { return networkStats; }
            set
            {
                if (networkStats != value)
                {
                    networkStats = value;
                    OnPropertyChanged("CurrentNetworkStatus");
                }
            }
        }

        public string LocalStats
        {
            get { return localStats; }
            set
            {
                if (localStats != value)
                {
                    localStats = value;
                    OnPropertyChanged("LocalStats");
                }
            }
        }

        public string GlobalStats
        {
            get { return globalStats; }
            set
            {
                if (globalStats != value)
                {
                    globalStats = value;
                    OnPropertyChanged("GlobalStats");
                }
            }
        }

        public bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                OnPropertyChanged("Visible");
            }
        }

        public bool AllowClose
        {
            get { return allowClose; }
            protected set
            {
                allowClose = value;
                OnPropertyChanged("AllowClose");
            }
        }

        public SafeFilteredObservingCollection<Node> Peers
        {
            get { return peers; }
            set
            {
                peers = value;
                OnPropertyChanged("Peers");
            }
        }

        public Node Node
        {
            get { return node; }
            set
            {
                node = value;
                OnPropertyChanged("Node");
            }
        }

        public string Avatar
        {
            get { return avatar; }
            set
            {
                avatar = value;
                OnPropertyChanged("Avatar");
            }
        }

        public string WindowTitle
        {
            get { return windowTitle; }
            set
            {
                windowTitle = value;
                OnPropertyChanged("WindowTitle");
            }
        }

        public string NetworkStatus
        {
            get { return networkInfo; }
            set
            {
                networkInfo = value;
                OnPropertyChanged("NetworkStatus");
            }
        }

        public string CurrentChatMessage
        {
            get { return currentChatMessage; }
            set
            {
                currentChatMessage = value;
                OnPropertyChanged("CurrentChatMessage");
            }
        }

        public string Nickname
        {
            get { return nickname; }
            set
            {
                nickname = value;
                OnPropertyChanged("Nickname");
            }
        }

        public string Description
        {
            get { return description; }
            set
            {
                description = value;
                OnPropertyChanged("Description");
            }
        }

        public SafeObservingCollection<TransferSession> Sessions
        {
            get { return sessions; }
            set
            {
                sessions = value;
                OnPropertyChanged("Sessions");
            }
        }

        public SafeObservingCollection<string> ChatMessages
        {
            get { return chatList; }
            set
            {
                chatList = value;
                OnPropertyChanged("ChatMessages");
            }
        }

        public ICommand Chat
        {
            get { return chat; }
            set
            {
                chat = value;
                OnPropertyChanged("Chat");
            }
        }

        public ICommand Search
        {
            get { return search; }
            set
            {
                search = value;
                OnPropertyChanged("Search");
            }
        }

        public ICommand OpenExternal
        {
            get { return openExternal; }
            set
            {
                openExternal = value;
                OnPropertyChanged("OpenExternal");
            }
        }

        public ICommand Compare
        {
            get { return compare; }
            set
            {
                compare = value;
                OnPropertyChanged("Compare");
            }
        }

        public ICommand UserInfo
        {
            get { return userinfo; }
            set
            {
                userinfo = value;
                OnPropertyChanged("UserInfo");
            }
        }

        public ICommand Closing
        {
            get { return closing; }
            set
            {
                closing = value;
                OnPropertyChanged("Closing");
            }
        }

        public ICommand ViewQueue
        {
            get { return viewQueue; }
            set
            {
                viewQueue = value;
                OnPropertyChanged("ViewQueue");
            }
        }

        public ICommand EditShares
        {
            get { return editShares; }
            set
            {
                editShares = value;
                OnPropertyChanged("EditShares");
            }
        }

        public ICommand SendChatMessage
        {
            get { return sendChatMessage; }
            set
            {
                sendChatMessage = value;
                OnPropertyChanged("SendChatMessage");
            }
        }

        public ICommand Settings
        {
            get { return settings; }
            set
            {
                settings = value;
                OnPropertyChanged("Settings");
            }
        }

        public ICommand ViewShare
        {
            get { return viewShare; }
            set
            {
                viewShare = value;
                OnPropertyChanged("ViewShare");
            }
        }

        public object SelectedClient
        {
            get { return selectedClient; }
            set
            {
                selectedClient = value;
                OnPropertyChanged("SelectedClient");
            }
        }

        public Dispatcher Dispatcher
        {
            get { return ViewCore.Dispatcher; }
        }

        public void DoFlashWindow()
        {
            ViewCore.Flash();
        }


        public void Show()
        {
            visible = true;
            ViewCore.Show();
        }

        public void Close()
        {
            visible = false;
            AllowClose = true;
            ViewCore.Close();
        }
    }
}