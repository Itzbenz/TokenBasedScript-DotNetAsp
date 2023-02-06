using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TokenBasedScript.Models;

public class ScriptExecution : ITrackableEntity
{
    [Key] public string Id { get; set; } = "";
    public User? User { get; set; }
    public string? ScriptName { get; set; }
    public string? ScriptContent { get; set; }
    public IList<Status> Statuses { get; set; } = new List<Status>();
    public bool IsSuccess { get; set; }
    public bool IsFinished { get; set; }
    public float InternalProgress { get; set; }

    [NotMapped]
    public float Progress
    {
        get => IsFinished ? 1 : InternalProgress;
        set => InternalProgress = Math.Clamp(value, 0f, 1f);
    }

    public int TokenUsed { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }

    public class Status : ITrackableEntity
    {
        [Key] public int Id { get; set; }
        public string? Message { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
    }
}