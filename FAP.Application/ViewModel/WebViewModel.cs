using FAP.Application.Views;
using FAP.Application.ViewModels;

namespace FAP.Application.ViewModel
{
    public class WebViewModel : ViewModelBase<IWebPanel>
    {
        public WebViewModel(IWebPanel view)
            : base(view)
        {
        }

        public string Location
        {
            set { ViewCore.Location = value; }
            get { return ViewCore.Location; }
        }
    }
}