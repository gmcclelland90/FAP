using System;
using System.Collections.ObjectModel;
using System.Text;
using Fap.Foundation;
using System.Text.Json.Serialization;

namespace FAP.Domain.Entities.FileSystem
{
    public class BrowsingFile : BaseEntity
    {
        private bool populated;
        private ObservableCollection<BrowsingFile> subItems = new ObservableCollection<BrowsingFile>();
        private BrowsingFile temp = null!;

        [JsonIgnore]
        public ObservableCollection<BrowsingFile> Items
        {
            set { subItems = value; }
            get { return subItems; }
        }


        [JsonIgnore]
        public FilteredObservableCollection<BrowsingFile> Folders
        {
            get
            {
                if (!IsPopulated && subItems.Count == 0)
                {
                    temp = new BrowsingFile {IsFolder = true};
                    subItems.Add(temp);
                }
                var lcv = new FilteredObservableCollection<BrowsingFile>(subItems);
                lcv.Filter = i => (i).IsFolder;
                return lcv;
            }
        }

        public bool IsPopulated
        {
            set
            {
                populated = value;
                if (value)
                {
                    if (subItems.Contains(temp))
                        subItems.Remove(temp);
                }
            }
            get { return populated; }
        }

        [System.Runtime.Serialization.DataMember]
        public bool IsFolder { set; get; }
        [System.Runtime.Serialization.DataMember]
        public string Name { set; get; } = string.Empty;
        [System.Runtime.Serialization.DataMember]
        public long Size { set; get; }
        [System.Runtime.Serialization.DataMember]
        public DateTime LastModified { set; get; }

        public string Extension
        {
            get
            {
                if (null == Name)
                    return string.Empty;
                return System.IO.Path.GetExtension(Name);
            }
        }

        [JsonIgnore]
        public string FullPath
        {
            get
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(Path))
                {
                    sb.Append(Path);
                    sb.Append("/");
                }
                sb.Append(Name);
                return sb.ToString();
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    // Don't set anything if the value is null or empty
                    return;
                }
                
                if (value.Contains("/"))
                {
                    int split = value.LastIndexOf("/");
                    Path = value.Substring(0, split);
                    Name = value.Substring(split + 1, value.Length - (split + 1));
                }
                else
                {
                    Name = value;
                }
            }
        }

        [System.Runtime.Serialization.DataMember]
        public string Path { set; get; } = string.Empty;

        public void AddItem(BrowsingFile ent)
        {
            subItems.Add(ent);
        }

        public void ClearItems()
        {
            if (subItems.Count > 0)
                subItems.Clear();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}