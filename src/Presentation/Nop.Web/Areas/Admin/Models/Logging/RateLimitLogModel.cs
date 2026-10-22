using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Logging;

/// <summary>
/// Represents a rate limit log model
/// </summary>
public partial record RateLimitLogModel : BaseNopEntityModel
{
    #region Properties

    /// <summary>
    /// Gets or sets the short message
    /// </summary>
    public string ShortMessage { get; set; }

    /// <summary>
    /// Gets or sets the date and time of instance creation
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the log level
    /// </summary>
    public string LogLevel { get; set; }

    #endregion
}

/// <summary>
/// Represents a rate limit log list model
/// </summary>
public partial record RateLimitLogListModel : BaseNopModel
{
    #region Ctor

    public RateLimitLogListModel()
    {
        Logs = new List<RateLimitLogModel>();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the rate limit log items
    /// </summary>
    public IList<RateLimitLogModel> Logs { get; set; }

    /// <summary>
    /// Gets or sets the total number of rate limit log items
    /// </summary>
    public int TotalCount { get; set; }

    #endregion
}
