using Microsoft.AspNetCore.Identity;

namespace TokenBasedScript.Models;
using System.ComponentModel.DataAnnotations;



    public class User : IdentityUser, ITrackableEntity
    {
   
        public string? Snowflake { get; set; }
       
   
      
        public bool IsAdmin { get; set; }

        public int TokenLeft { get; set; }


        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
    }
