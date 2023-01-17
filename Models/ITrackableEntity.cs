namespace TokenBasedScript.Models;

public interface ITrackableEntity
{
    
    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }
}