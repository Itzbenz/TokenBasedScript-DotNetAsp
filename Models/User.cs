namespace TokenBasedScript.Models;
using System.ComponentModel.DataAnnotations;



    public class User
    {
        [Key]
        public int Id { get; set; }
        //Discord Snowflake
        public string? Snowflake { get; set; }
        [Required]
        public string? UserName { get; set; }
        [Required]
        public string? Email { get; set; }

        [DataType(DataType.Date)]
        public DateTime LastLogin { get; set; }
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; }

        public bool IsAdmin { get; set; }

        public int TokenLeft { get; set; }

    }
