using System.ComponentModel.DataAnnotations;

namespace TokenBasedScript.Models;

public class Settings : ITrackableEntity
{
    
    [Key]
    public string Name { get; set; }
    public string Type { get; set; }//HTML: text, number, checkbox, select, textarea
    public string? DefaultValueString { get; set; }
    public string? ValueString { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }


    
    
}