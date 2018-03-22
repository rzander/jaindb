// https://stackoverflow.com/questions/45805411/asp-net-core-2-0-authentication-middleware

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using jaindb;
using Microsoft.AspNetCore.Authentication;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CustomAuthExtensions
    {
        public static AuthenticationBuilder AddCustomAuth(this AuthenticationBuilder builder, Action<CustomAuthOptions> configureOptions)
        {
            return builder.AddScheme<CustomAuthOptions, CustomAuthHandler>("Custom Scheme", "Custom Auth", configureOptions);
        }
    }
}
