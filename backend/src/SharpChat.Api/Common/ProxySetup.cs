using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SharpChat.Api.Common;

internal static class ProxySetup
{
    private static readonly string[] PrivateNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"];

    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var network in PrivateNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

    public static IApplicationBuilder UseInstanceHeader(this IApplicationBuilder app, string instanceName) =>
        app.Use((context, next) =>
        {
            context.Response.Headers["X-SharpChat-Instance"] = instanceName;
            return next(context);
        });
}
