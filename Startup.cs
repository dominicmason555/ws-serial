using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;

namespace ws_serial
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseWebSockets();
            app.UseHttpsRedirection();
            app.UseMvc();

            app.Map("/ws", builder =>
            {
                builder.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        var cts = new CancellationTokenSource();

                        var tasks = new List<Task>()
                            {
                                SendTask(webSocket, cts.Token),
                                ReceiveTask(webSocket, cts.Token)
                            };

                        Console.WriteLine("Starting Tasks");
                        await Task.WhenAny(tasks);
                        Console.WriteLine("Task Finished, cancelling");
                        cts.Cancel();
                        Console.WriteLine("Cancelled");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);

                        return;
                    }
                    await next();
                });
            });
        }
        private async Task ReceiveTask(WebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[1024 * 4];
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            while (!result.CloseStatus.HasValue)
            {
                var resArray = new ArraySegment<byte>(buffer, 0, result.Count);
                Console.WriteLine(Encoding.Default.GetString(resArray));

                await socket.SendAsync(resArray, result.MessageType, result.EndOfMessage, token);
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            }
        }
        private async Task SendTask(WebSocket socket, CancellationToken token)
        {
            var random = new Random();
            var lastNum = 0.0;
            var counter = 0;
            while (true)
            {
                counter += 1;
                lastNum += random.NextDouble() - 0.5;
                var text = String.Format("{{\"point1\": {0} }}", lastNum);
                if (counter >= 5) {
                    text = String.Format("{{\"point1\": {0}, \"point2\": {0} }}", lastNum);
                    counter = 0;
                }

                var message = Encoding.Default.GetBytes(text);

                await socket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, token);
                await Task.Delay(1);
            }
        }
    }
}
