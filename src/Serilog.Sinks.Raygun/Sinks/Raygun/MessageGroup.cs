namespace Serilog.Sinks.Raygun
{
  /// <summary>
  /// Class MessageGroup.
  /// </summary>
  /// <seealso cref="Serilog.Sinks.Raygun.IMessageGroup" />
  public class MessageGroup : IMessageGroup
  {
    /// <summary>
    /// The custom group key
    /// </summary>
    /// <value>The group key.</value>
    public string GroupKey { get; set; }

    /// <summary>
    /// Defaults group key to empty
    /// </summary>
    public MessageGroup()
    {
      GroupKey = string.Empty;
    }
  }
}
