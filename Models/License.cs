using System.ComponentModel.DataAnnotations;

namespace TokenBasedScript.Models;

public class License : ITrackableEntity
{
    public int Id { get; set; }
    public User? User { get; set; }
    public string Code { get; set; }
    public string? ExternalId { get; set; }
    
    //security, 1 device per license
    public string? Hwid { get; set; }
    public string? DeviceName { get; set; }
    public string? IpHash { get; set; }
    public DateTime? DateModifiedIpHash { get; set; }
    public DateTime? DateModifiedHwid { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.Now;
    public DateTime DateModified { get; set; } = DateTime.Now;
}