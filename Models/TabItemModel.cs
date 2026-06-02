#nullable disable

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickSurfBrowser.Models
{
    public class TabItemModel : INotifyPropertyChanged
    {
        private string _title;
        private string _url;
        private bool _isSelected;

        public int Id { get; }
        
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        
        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public TabItemModel(int id, string title, string url = "")
        {
            Id = id;
            _title = title ?? "";
            _url = url ?? "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}