namespace FAP.Shared.Interfaces
{
    public interface INode
    {
        string ID { get; set; }
        string Location { get; set; }
        string Secret { get; set; }
        string OverlordID { get; set; }
        int LastUpdate { get; set; }
    }
} 