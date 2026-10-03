using EagleEye.Service;
using EagleEye.Service.Communication;
using EagleEye.Shared.Constants;

// Runs as a Windows service when started by the SCM, and as a console app otherwise
// (development mode). UseWindowsService() also sets the content root to the install folder.
var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "EagleEyeService")
    .ConfigureWebHostDefaults(webBuilder =>
    {
        // US-001: plain HTTP, bound to the loopback interface only. Remote (parent app)
        // connectivity with TLS and pairing comes in a later story.
        webBuilder.UseKestrel(kestrel => kestrel.ListenLocalhost(ServiceDefaults.ServicePort));

        webBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IVersionProvider>(
                new AssemblyVersionProvider(typeof(AssemblyVersionProvider).Assembly));
            services.AddSignalR();
        });

        webBuilder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapHub<TrayHub>(HubRoutes.Tray));
        });
    })
    .Build();

await host.RunAsync();
