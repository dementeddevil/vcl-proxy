using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Im.Proxy
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IInboundRuleResolver, InboundRuleResolver>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async (context) =>
            {
                var inboundRuleResolver = context
                    .RequestServices
                    .GetService<IInboundRuleResolver>();
                var uri = new UriBuilder(
                    context.Request.Scheme,
                    context.Request.Host.Host,
                    context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80),
                    context.Request.Path.ToString()).Uri;
                var inboundAction = inboundRuleResolver.Get(uri);
                if (inboundAction == null)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Appropriate route not found.");
                    return;
                }

                // Check for redirect
                if (inboundAction.ActionKind == InboundRuleActionKind.RedirectPermanent)
                {

                }

                await context.Response.WriteAsync("Hello World!");
            });
        }
    }

    public class InboundRuleResolver : IInboundRuleResolver
    {
        private readonly List<Tuple<InboundRuleMatch, InboundRuleAction>> _routes = new List<Tuple<InboundRuleMatch, InboundRuleAction>>();

        public InboundRuleAction Get(Uri uri)
        {
            return _routes.FirstOrDefault(r => r.Item1.IsMatch(uri))?.Item2;
        }
    }

    public class InboundRuleMatch
    {
        private readonly Regex _uriMatcher;

        public InboundRuleMatch(string uriRegularExpression)
        {
            _uriMatcher = new Regex(
                uriRegularExpression,
                RegexOptions.Compiled |
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        public bool IsMatch(Uri uri)
        {
            return _uriMatcher.IsMatch(uri.GetLeftPart(UriPartial.Path));
        }
    }

    public class InboundRuleAction
    {
        public InboundRuleActionKind ActionKind { get; set; }
    }

    public enum InboundRuleActionKind
    {
        ReturnStatus,
        Rewrite,
        RedirectTemporary,
        RedirectPermanent
    }
}
