using System;
using System.Windows.Input;
using FAP.Application.Views;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Foundation;

namespace FAP.Application.ViewModel
{
    public class SearchViewModel : ViewModelBase<ISearchView>
    {
        private bool allowSearch = true;
        private ICommand download;
        private string lowerStatusMessage;
        private DateTime? modifiedDate;
        private string modifiedSearchType;
        private ICommand reset;
        private SafeObservingCollection<SearchResult> results;
        private ICommand search;
        private string searchString;
        private string sizeModifier;
        private string sizeSearchType;
        private double? sizeText;
        private string upperStatusMessage;
        private ICommand viewShare;

        public SearchViewModel(ISearchView v) : base(v)
        {
        }


        public bool AllowSearch
        {
            get { return allowSearch; }
            set
            {
                allowSearch = value;
                OnPropertyChanged("AllowSearch");
            }
        }

        public string SizeSearchType
        {
            get { return sizeSearchType; }
            set
            {
                sizeSearchType = value;
                OnPropertyChanged("SizeSearchType");
            }
        }

        public string SizeModifier
        {
            get { return sizeModifier; }
            set
            {
                sizeModifier = value;
                OnPropertyChanged("SizeModifier");
            }
        }

        public double? SizeText
        {
            get { return sizeText; }
            set
            {
                sizeText = value;
                OnPropertyChanged("SizeText");
            }
        }

        public string ModifiedSearchType
        {
            get { return modifiedSearchType; }
            set
            {
                modifiedSearchType = value;
                OnPropertyChanged("ModifiedSearchType");
            }
        }

        public DateTime? ModifiedDate
        {
            get { return modifiedDate; }
            set
            {
                modifiedDate = value;
                OnPropertyChanged("ModifiedDate");
            }
        }


        public ICommand Reset
        {
            get { return reset; }
            set
            {
                reset = value;
                OnPropertyChanged("Reset");
            }
        }

        public ICommand Download
        {
            get { return download; }
            set
            {
                download = value;
                OnPropertyChanged("Download");
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

        public ICommand Search
        {
            set
            {
                search = value;
                OnPropertyChanged("Search");
            }
            get { return search; }
        }

        public string SearchString
        {
            set
            {
                searchString = value;
                OnPropertyChanged("SearchString");
            }
            get { return searchString; }
        }

        public SafeObservingCollection<SearchResult> Results
        {
            get { return results; }
            set
            {
                results = value;
                OnPropertyChanged("Results");
            }
        }

        public string LowerStatusMessage
        {
            get { return lowerStatusMessage; }
            set
            {
                lowerStatusMessage = value;
                OnPropertyChanged("LowerStatusMessage");
            }
        }

        public string UpperStatusMessage
        {
            get { return upperStatusMessage; }
            set
            {
                upperStatusMessage = value;
                OnPropertyChanged("UpperStatusMessage");
            }
        }
    }
}