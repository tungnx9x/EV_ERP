using EV_ERP.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EV_ERP.Filters
{
    /// <summary>
    /// Redirects to login page if no valid session found.
    /// Apply to controllers or actions that require authentication.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var user = session.GetObject<CurrentUser>(SessionKeys.CurrentUser);

            if (user == null)
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth",
                    new { returnUrl });
                return;
            }

            base.OnActionExecuting(context);
        }
    }

    /// <summary>
    /// Requires a specific role. Redirects to 403 if role not matched.
    /// Must be combined with RequireLogin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireRoleAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RequireRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var user = session.GetObject<CurrentUser>(SessionKeys.CurrentUser);

            if (user == null || !_roles.Contains(user.RoleCode, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
