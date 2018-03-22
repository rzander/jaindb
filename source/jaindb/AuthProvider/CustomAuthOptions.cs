// https://stackoverflow.com/questions/45805411/asp-net-core-2-0-authentication-middleware

using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace jaindb
{
    public class CustomAuthOptions : AuthenticationSchemeOptions
    {
        public CustomAuthOptions()
        {

        }
    }
}
