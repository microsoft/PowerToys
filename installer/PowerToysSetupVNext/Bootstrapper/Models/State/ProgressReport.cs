namespace Bootstrapper.Models.State
{
  internal class ProgressReport
  {
    public string Message { get; set; }
    public string PackageName { get; set; }
    public int Progress { get; set; }
  }
}