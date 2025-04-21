using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;


using Microsoft.SemanticKernel.Plugins.OpenApi;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Web;

namespace OpenAPI
{
    internal class Program
    {
        private static string OPENAI_APIKEY = null;
        private static string OPENAI_ORGID = null;
        private static string KERNEL_MEMORY_APIKEY = null;



        static async Task Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            OPENAI_APIKEY = config["OpenAI:ApiKey"];
            OPENAI_ORGID = config["OpenAI:OrgId"];
            KERNEL_MEMORY_APIKEY = config["KernelMemory:ApiKey"];

            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: "gpt-4o-mini",
                    //modelId: "gpt-4o",
                    //modelId: "gpt-4.1",
                    //modelId: "o4-mini",
                    apiKey: OPENAI_APIKEY,
                    httpClient: HttpLogger.GetHttpClient(false));

            var kernel = builder.Build();
            await RunTest_AndrewShop_Carts(kernel);
            //await RunTest_AndrewKM_SearchAPI(kernel);

        }

        private static async Task RunTest_AndrewShop_Carts(Kernel kernel)
        {
            //
            //  given info (context)
            //
            string api_spec_uri = "https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json";

            // chatgpt 生成測試案例
            // https://chatgpt.com/share/68009f63-8088-800d-8f88-768ba2b2e00d
            string test_case_prompts =
                // system prompt
                """
                <message role="system">
                依照我給你的 test case 執行測試。
                測試案例分為四個部分:

                - Context:  此為 API 執行的環境設置, 包含 shop id, user access token, location id 等等。請用 ApiExecutionContextPlugin 來設置這些環境變數。
                - Given:    執行測試的前置作業。進行測試前請先完成這些步驟。若 Given 的步驟執行失敗，請標記該測試 [無法執行]
                - When:     測試步驟，請按照敘述執行。若這些步驟無法完成，請標記該測試 [執行失敗], 並且附上失敗的原因說明。
                - Then:     預期結果，請檢查這些步驟的執行結果是否符合預期。若這些步驟無法完成，請標記該測試 [測試不過], 並且附上不符合預期的原因說明。如果測試符合預期結果，請標記該測試 [測試通過]
                </message>
                """
                +

                // user prompt: given, when, then
                """
                <message role="user">

                ## Context
                
                - shop: shop123
                - user: { user: andrew, password: 123456 }
                - location: { id: zh-TW, time-zone: UTC+8, currency: TWD }


                ## Given

                - 測試前請清空購物車
                - 指定商品為 productID: 3
                

                ## When

                Test name: TC‑05 （非法上界：qty = 11）
                step 1, 嘗試加入 11 件 (qty=11)
                step 2, 檢查購物車內容
                

                ## Then

                - 執行 step 1 時，伺服器應回傳 400 Bad Request（數量超出 10）
                - step 2 查詢結果，購物車應該是空的

                </message>
                """
                +

                // report and summary requirement
                """
                <message role="user">
                請輸出 markdown 的測試報告給我，包含下列資訊:

                1. 我需要每個步驟的執行狀況說明
                2. 以及最終執行成果說明
                3. 報告請附上 json format result, 格式如下:
                {
                    "result": 無法執行(start_fail) | 執行失敗(exec_fail) | 測試不過(test_fail) | 測試通過(test_pass),
                    "comments": "測試執行結果說明",
                    "context":{
                        "shop": "shop1",
                        "user": { "access_token": "xxxx", "user": "andrew" },
                        "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
                    },
                    "steps": [
                        { "api": "api-name", "request": {}, "response": {}, "test-result"="註記該步驟執行結果是否符合預期，pass or failure", "test-comments"="測試結果說明" }
                    ];
                }
                4. 輸出能執行這段測試案例的 C# 程式碼, 用 xUnit 框架的結構來產生程式碼
                </message>
                """;


            APIExecutionContextPlugin.Init_OAuth2(
                "0000", 
                Guid.NewGuid().ToString("N"), 
                "https://andrewshopoauthdemo.azurewebsites.net/api/login/authorize", 
                "https://andrewshopoauthdemo.azurewebsites.net/api/login/token");


            kernel.Plugins.AddFromType<APIExecutionContextPlugin>();

            await kernel.ImportPluginFromOpenApiAsync(
               pluginName: "andrew_shop",
               uri: new Uri(api_spec_uri),
               executionParameters: new OpenApiFunctionExecutionParameters()
               {
                   // Determines whether payload parameter names are augmented with namespaces.
                   // Namespaces prevent naming conflicts by adding the parent parameter name
                   // as a prefix, separated by dots
                   EnablePayloadNamespacing = true,
                   HttpClient = HttpLogger.GetHttpClient(true),
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

            


            var settings = new PromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            //
            // 直接將文字敘述的測試案例交給 AI 自動執行，並產生測試報告
            //
            Console.WriteLine(await kernel.InvokePromptAsync<string>(
                test_case_prompts,
                new(settings)));


            //
            //  只自動化執行腳本
            //
            //Console.WriteLine(await kernel.InvokePromptAsync<string>(
            //    """
            //    用測試帳號 happy / 12341234 登入

            //    step 1, 列出測試者 (會員) 的基本資訊
            //    step 2, 清空購物車
            //    step 3, 查詢販售中的商品清單
            //    step 4, 按照下列清單，將商品加入購物車:
            //    - 可樂, 1件
            //    - 啤酒, 2件
            //    - 綠茶, 3件
            //    step 5, 移除 啤酒, 1件
            //    step 6, 檢查購物車內容, 是否符合下列期待:
            //    - 可樂, 1件
            //    - 啤酒, 1件
            //    - 綠茶, 3件
            //    - 結帳金額: 543元

            //    直接用 json 輸出 api execute context, 以及購物車最終狀態。購物車內容請附加上 [完整的商品名稱]
            //    """,
            //    new(settings)));


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
        public static async Task<string> SetShopAsync([Description("provide shop apikey, if pass the validation, the shopid will be set.")]string shopApiKey)
        {
            Console.WriteLine($"SetShop APIKEY: {shopApiKey}");

            ShopID = "shop8";
            return ShopID;
        }

        [KernelFunction]
        [Description("Set the current location info to the API execution context.")]
        public static async Task<string> SetLocation([Description("iso location id format, ex: zh-TW, en-US.")]string locationId)
        {
            Console.WriteLine($"SetLocation: {locationId}");

            LocationID = locationId;
            return LocationID;
        }



        [KernelFunction]
        [Description("Set the AccessToken for the API execution context via OAuth2 user authorize process.")]
        public static async Task<string> SetUserAccessTokenAsync(
            [Description("login user name")]string username, 
            [Description("login user password")]string password)
        {
            Console.WriteLine($"Set Access Token (via user login: {username} / {password} )");
            if (!_isInitialized)
            {
                Console.WriteLine("APIExecutionContextPlugin is not initialized.");
                return null;
            }

            //string access_token = null;

            //string client_id = "0000";
            //string client_secret = "86ec4647-c114-4cca-b2ac-4fd6bcf6eb0d";
            string redirect_uri = "app://oauth2.dev/callback";

            //const string authorize_uri = "https://andrewshopoauthdemo.azurewebsites.net/api/login/authorize";
            //const string token_uri = "https://andrewshopoauthdemo.azurewebsites.net/api/login/token";

            //string name = "andrew";
            //string password = "123456";

            //bool success = false;

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

                //Console.WriteLine($"code:  {nvs["code"]}");
                //Console.WriteLine($"state: {nvs["state"]}");

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
                Console.WriteLine($"auth:  {UserAccessToken}");
                //success = true;
                return UserAccessToken;
            }

            return null;
        }

    }
}
