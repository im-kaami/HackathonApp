using System;
using System.ComponentModel.DataAnnotations;

namespace HackathonData.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string TeamName { get; set; } = null!;

        [Required, MaxLength(120)]
        public string ProjectName { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Category { get; set; } = null!;

        public DateTime EventDate { get; set; }

        public decimal Score { get; set; }

        public int Members { get; set; }

        [Required, MaxLength(100)]
        public string Captain { get; set; } = null!;
    }
}
