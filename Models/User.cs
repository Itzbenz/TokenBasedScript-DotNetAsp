using Microsoft.AspNetCore.Identity;

namespace TokenBasedScript.Models;

using System.ComponentModel.DataAnnotations;

public class User : IdentityUser, ITrackableEntity
{
    public string? Snowflake { get; set; }


    public bool IsAdmin { get; set; }

    public long TokenLeft { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime DateCreated { get; set; }  = DateTime.Now;
    public DateTime DateModified { get; set; }  = DateTime.Now;
}