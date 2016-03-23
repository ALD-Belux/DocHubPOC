namespace DocHubPOC.Models
{
    /// <summary>
    /// Represent an Item stored in a container (logical/security partition)
    /// </summary>
    public class FileItem
    {
        public string Container { get; set; }
        public string Id { get; set; }

        public string Uid
        {
            get
            {
                return Container + Id;
            }
        }
    }
}