using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;


namespace OpenAPI_MCP
{
    public class Program
    {
        private static string OPENAI_APIKEY;

        static async Task Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            OPENAI_APIKEY = config["OpenAI:ApiKey"];


            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    //modelId: "gpt-4o-mini",
                    //modelId: "gpt-4o",
                    //modelId: "gpt-4.1",
                    modelId: "o4-mini",
                    apiKey: OPENAI_APIKEY,
                    httpClient: HttpLogger.GetHttpClient(false));

            var kernel = builder.Build();

            // 指定測試程式的 oauth2 client id / secret / auth & callback uri
            APIExecutionContextPlugin.Init_OAuth2(
                "0000",
                Guid.NewGuid().ToString("N"),
                "https://andrewshopoauthdemo.azurewebsites.net/api/login/authorize",
                "https://andrewshopoauthdemo.azurewebsites.net/api/login/token");

            APIExecutionContextPlugin.SetUserAccessTokenAsync("andrew", "1234").Wait();

            // 將 APIExecutionContextPlugin 加入到 kernel 中 ( 提供 AI 可用的 tool )
            kernel.Plugins.AddFromType<APIExecutionContextPlugin>();

            // 將待測的 API ( via swagger ) 轉成 Plugin 加入到 kernel 中 ( 提供 AI 可用的 tool )
#pragma warning disable SKEXP0040 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await kernel.ImportPluginFromOpenApiAsync(
               pluginName: "andrew_shop",
               uri: new Uri("https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json"),
               executionParameters: new OpenApiFunctionExecutionParameters()
               {
                   EnablePayloadNamespacing = true,
                   HttpClient = HttpLogger.GetHttpClient(false),
                   AuthCallback = (request, cancel) =>
                   {
                       var api_context = APIExecutionContextPlugin.GetContext();
                       // Add the authorization header to the request
                       request.Headers.Add($"Authorization", $"Bearer {api_context.UserAccessToken}");
                       // TODO: set location info in Headers
                       // TODO: set tenant-id info in Headers
                       return Task.CompletedTask;
                   },
               }
            );
#pragma warning restore SKEXP0040 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.



            var host_builder = Host.CreateEmptyApplicationBuilder(null);

            host_builder.Services
                //.AddLogging(logging =>
                //{
                //    logging.AddConsole();
                //    logging.SetMinimumLevel(LogLevel.Trace);
                //})
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools(kernel);

            var mcphost = host_builder.Build();
            mcphost.Run();
        }








    }


    public static class MCPToolsExtension
    {
        public static IMcpServerBuilder WithTools(this IMcpServerBuilder builder, Kernel kernel)
        {
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    builder.Services.AddSingleton(services => McpServerTool.Create(function.AsAIFunction()));

                    //if (function.Name == "GetCart")
                    //{
                    //    var args = new KernelArguments();
                    //    args.Add("id", 40);
                    //    var result = function.InvokeAsync(kernel, args).Result;
                    //}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
            }

            return builder;
        }
    }



    public class APIExecutionContextPlugin
    {
        public static string ShopID { get; private set; } = "shop8";

        public static string UserAccessToken { get; private set; } = null;

        public static string LocationID { get; private set; } = "zh-TW";

        private static bool _isInitialized = false;
        private static string _client_id = null;
        private static string _client_secret = null;
        private static string _authorize_uri = null;
        private static string _token_uri = null;

        public static bool Init_OAuth2(string clientId, string clientSecret, string authorizeUri, string tokenUri)
        {
            if (_isInitialized)
            {
                Console.WriteLine("APIExecutionContextPlugin is already initialized.");
                return false;
            }

            _client_id = clientId;
            _client_secret = clientSecret;
            _authorize_uri = authorizeUri;
            _token_uri = tokenUri;

            _isInitialized = true;

            return true;
        }


        [KernelFunction]
        [Description("Get the context for the API execution. Include current tenant (shop id), current use (access token) and current location (iso location-id).")]
        public static (string ShopID, string UserAccessToken, string LocationID) GetContext()
        {
            return (ShopID, UserAccessToken, LocationID);
        }

        [KernelFunction]
        [Description("Set the current tenant (shop id) for the API execution context.")]
        public static async Task<string> SetShopAsync([Description("provide shop apikey, if pass the validation, the shopid will be set.")] string shopApiKey)
        {
            Console.WriteLine($"SetShop APIKEY: {shopApiKey}");

            ShopID = "shop8";
            return ShopID;
        }

        [KernelFunction]
        [Description("Set the current location info to the API execution context.")]
        public static async Task<string> SetLocation([Description("iso location id format, ex: zh-TW, en-US.")] string locationId)
        {
            Console.WriteLine($"SetLocation: {locationId}");

            LocationID = locationId;
            return LocationID;
        }

        [KernelFunction]
        [Description("Set the AccessToken for the API execution context via OAuth2 user authorize process.")]
        public static async Task<string> SetUserAccessTokenAsync(
            [Description("login user name")] string username,
            [Description("login user password")] string password)
        {
            //Console.Error.WriteLine($"Set Access Token (via user login: {username} / {password} )");
            if (!_isInitialized)
            {
                //Console.Error.WriteLine("APIExecutionContextPlugin is not initialized.");
                return null;
            }

            string redirect_uri = "app://oauth2.dev/callback";

            HttpClient hc = HttpLogger.GetHttpClient(false);
            HttpResponseMessage response = await hc.PostAsync(
                _authorize_uri,
                new StringContent(
                    $"client_id={_client_id}&redirect_uri={redirect_uri}&state={_client_secret}&name={username}&password={password}",
                    MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded")));

            if (response.Headers.TryGetValues("Location", out var locations))
            {
                var callback = new Uri(locations.First());
                var nvs = HttpUtility.ParseQueryString(callback.Query);

                string code = nvs["code"];
                string state = nvs["state"];

                //Console.Error.WriteLine($"code:  {nvs["code"]}");
                //Console.Error.WriteLine($"state: {nvs["state"]}");

                response = await hc.PostAsync(
                    _token_uri,
                    new StringContent(
                        $"code={code}",
                        MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded")
                    ));

                // response 格式是如下的 json: {"access_token":"a5862036f8fd46b5b0168042108817c2","token_type":"Bearer","expires_in":3600}
                // 所以要用 System.Text.Json 解析 access_token
                string json = await response.Content.ReadAsStringAsync();
                UserAccessToken = System.Text.Json.JsonDocument.Parse(json)
                    .RootElement
                    .GetProperty("access_token")
                    .GetString();
                //Console.Error.WriteLine($"auth:  {UserAccessToken}");
                return UserAccessToken;
            }

            return null;
        }
    }

}
