using Microsoft.AspNetCore.Mvc;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Web.Areas.Admin.Components;

/// <summary>
/// Represents a view component that displays recent rate limit log entries
/// </summary>
public partial class RateLimitLogViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ILogModelFactory _logModelFactory;
    protected readonly IPermissionService _permissionService;

    #endregion

    #region Ctor

    public RateLimitLogViewComponent(ILogModelFactory logModelFactory,
        IPermissionService permissionService)
    {
        _logModelFactory = logModelFactory;
        _permissionService = permissionService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke view component
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the view component result
    /// </returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.System.MANAGE_SYSTEM_LOG))
            return Content(string.Empty);

        var model = await _logModelFactory.PrepareRateLimitLogListModelAsync(5);

        return await ViewAsync(model);
    }

    #endregion
}
