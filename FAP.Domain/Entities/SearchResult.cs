using System;
using Newtonsoft.Json;

namespace FAP.Domain.Entities
{
    public class SearchResult : BaseEntity
    {
        private string fileName = string.Empty;
        private DateTime modified;
        private string path = string.Empty;
        private long size;
        private string user = string.Empty;

        public bool IsFolder { get; set; }

        public string ClientID { get; set; } = string.Empty;

        public string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                NotifyChange("FileName");
            }
        }

        public long Size
        {
            get { return size; }
            set
            {
                size = value;
                NotifyChange("Size");
            }
        }

        [JsonIgnore]
        public string User
        {
            get { return user; }
            set
            {
                user = value;
                NotifyChange("User");
            }
        }

        public DateTime Modified
        {
            get { return modified; }
            set
            {
                modified = value;
                NotifyChange("Modified");
            }
        }

        public string Path
        {
            get { return path; }
            set
            {
                path = value;
                NotifyChange("Path");
            }
        }
    }
}