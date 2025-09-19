namespace Specifications.Models
{
    public class SpeceficationsModel
    {

        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public double Duration { get; set; } = 0; // in hours
        public Status StatusProgess { get; set; }
        public enum Status
        {
            InProgress,
            Completed,
            Pending
        }
       
    }
}
