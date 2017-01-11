namespace Serilog.Sinks.Raygun
{
  /// <summary>
  /// IMessageGroup interface utilized for Raygun custom grouping feature
  /// </summary>
  public interface IMessageGroup
  {
    /// <summary>
    /// The custom group key
    /// </summary>
    /// <value>The group key.</value>
    string GroupKey { get; set; }

  }
}
