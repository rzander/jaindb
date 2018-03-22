// https://stackoverflow.com/questions/45805411/asp-net-core-2-0-authentication-middleware

using jaindb;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace jaindb
{
    internal class CustomAuthHandler : AuthenticationHandler<CustomAuthOptions>
    {
        public CustomAuthHandler(IOptionsMonitor<CustomAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
            // store custom services here...
        }
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // build the claims and put them in "Context"; you need to import the Microsoft.AspNetCore.Authentication package
            AuthenticationTicket ot = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), "Custom Scheme");
            return AuthenticateResult.Success(ot);
        }
    }
}
