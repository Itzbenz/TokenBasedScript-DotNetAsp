namespace TokenBasedScript.Models;
using System.ComponentModel.DataAnnotations;



    public class Order : ITrackableEntity
    {
        [Key]
        public string Id { get; set; }
        //Discord Snowflake
        public string? UserSnowflake { get; set; }
        
        public long Quantity { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
    }
