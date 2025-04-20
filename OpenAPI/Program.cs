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
                    //modelId: "gpt-4o-mini",
                    //modelId: "gpt-4o",
                    //modelId: "gpt-4.1",
                    modelId: "o4-mini",
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
            //string access_token = await AndrewShop_OAuth2_Login("andrew", "123456");
            //string shop_id = "shop1"; // 這是 demo 的 shop id
            //string location = "zh-TW"; // 這是 demo 的 location id

            string api_spec_uri = "https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json";

            // chatgpt 生成測試案例
            // https://chatgpt.com/share/68009f63-8088-800d-8f88-768ba2b2e00d
            string test_case_prompts =
                """
                ## T19 移除商品（負數 qty）
                
                API Execute Context:
                - shop: shop123
                - user: { user: andrew, password: 123456 }
                - location: { id: zh-TW, time-zone: UTC+8, currency: TWD }


                Test Case:
                step 1, 建立購物車並加入 productId=2, qty=2
                - POST /api/carts/create → cartId
                - POST /api/carts/{cartId}/items body {"productId":2,"qty":2}
                
                step 2, 移除 1 件同商品
                - POST /api/carts/{cartId}/items body {"productId":2,"qty":-1}
                                
                step 3, 檢查購物車
                - GET /api/carts/{cartId}
                
                test result
                - 預期結果: lineItems 中 productId=2 的 qty=1
                - 實際結果
                - 測試是否通過判定
                

                --
                請輸出 markdown 的測試報告給我
                我需要每個步驟的執行狀況說明
                以及最終執行成果說明

                報告請附上 json format result, 格式如下:
                {
                    "result": true(pass) | false(fail),
                    "comments": "測試通過 or 測試失敗說明",
                    "context":{
                        "shop": "shop1",
                        "user": { "access_token": "xxxx", "user": "andrew", "password": "123456" },
                        "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
                    },
                    "steps": [
                        { "api": "api-name", parameters: {}, result="pass|failure", logs="" }
                    ];
                }

                --
                請輸出能執行這段測試案例的 C# 程式碼給我
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

            // 擷取 T19, 直接將文字敘述的測試案例交給 AI 自動執行
            Console.WriteLine(await kernel.InvokePromptAsync<string>(
                test_case_prompts,
                new(settings)));

            //Console.WriteLine(await kernel.InvokePromptAsync<string>(
            //    """
            //    step 0, 列出測試者 (會員) 的基本資訊
            //    step 1, 清空購物車
            //    step 2, 按照下列清單加入購物車:
            //    - pid:1, 1件
            //    - pid:2, 2件
            //    - pid:3, 3件
            //    step 3, 移除 pid:2, 1件
            //    step 4, 檢查購物車內容, 是否符合:
            //    - pid:1, 1件
            //    - pid:2, 1件
            //    - pid:3, 3件
            //    """,
            //    new(settings)));


            //Console.WriteLine(await kernel.InvokePromptAsync<string>(
            //    """
            //    <message role="system">
            //    你是個 QA 測試人員
            //    請依序執行下列步驟，並且用測試報告的格式輸出。
            //    若最終符合預期結果，請傳回 "測試通過"。

            //    我期待測是要能涵蓋這些期待，請產生對應的情境，確保涵蓋這些預期的結果:
            //    1. 要能驗證參數的邊界範圍。請組合所有可能，測試邊界內，高於邊界 (+1)，低於邊界 (-1) 的情況。
            //    2. 替我按照文件，預測這些 API 的回傳結果，並且檢查是否符合預期。
            //    </message>
            //    <message role="user">
            //    測試的主要情境:
            //    step 1, 列出測試者 (會員) 的基本資訊
            //    step 2, 清空購物車
            //    step 3, 按照下列清單加入購物車:
            //    (請幫我決定哪些商品要加減幾件到購物車)
            //    step 4, 檢查購物車內容, 是否符合:
            //    (請幫我預測最終購物車該有的商品清單)
            //    </message>
            //    <message role="user">
            //    已知有商品ID: 1, 2, 3
            //    購物車的每種商品數量合理範圍是 0 ~ 5，超出皆屬異常
            //    購物車的商品數量上限是 10，超出皆屬異常

            //    請直接幫我按照上面敘述生成測試案例，並且自動執行，然後給我測試報告。
            //    </message>
            //    """,
            //    new(settings)));



        }

        // do oauth2 process in andrewshop demo site
        private static async Task<string> AndrewShop_OAuth2_Login(string username, string password)
        {
            string access_token = null;

            string client_id = "0000";
            string client_secret = "86ec4647-c114-4cca-b2ac-4fd6bcf6eb0d";
            string redirect_uri = "app://oauth2.dev/callback";

            const string authorize_uri = "https://andrewshopoauthdemo.azurewebsites.net/api/login/authorize";
            const string token_uri = "https://andrewshopoauthdemo.azurewebsites.net/api/login/token";

            //string name = "andrew";
            //string password = "123456";

            //bool success = false;

            HttpClient hc = HttpLogger.GetHttpClient(true);
            HttpResponseMessage response = await hc.PostAsync(
                authorize_uri,
                new StringContent(
                    //"response_type=code&client_id=0000&redirect_uri=app://oauth2.dev/callback&state=86ec4647-c114-4cca-b2ac-4fd6bcf6eb0d&name=andrew&password=123456",
                    $"client_id={client_id}&redirect_uri={redirect_uri}&state={client_secret}&name={username}&password={password}",
                    MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded")));

            if (response.Headers.TryGetValues("Location", out var locations))
            {
                var callback = new Uri(locations.First());
                var nvs = HttpUtility.ParseQueryString(callback.Query);

                string code = nvs["code"];
                string state = nvs["state"];

                Console.WriteLine($"code: {nvs["code"]}");
                Console.WriteLine($"code: {nvs["state"]}");

                response = await hc.PostAsync(
                    token_uri,
                    new StringContent(
                        $"code={code}",
                        MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded")
                    ));

                // response 格式是如下的 json: {"access_token":"a5862036f8fd46b5b0168042108817c2","token_type":"Bearer","expires_in":3600}
                // 所以要用 System.Text.Json 解析 access_token
                string json = await response.Content.ReadAsStringAsync();
                access_token = System.Text.Json.JsonDocument.Parse(json)
                    .RootElement
                    .GetProperty("access_token")
                    .GetString();
                //success = true;
                return access_token;
            }

            return null;
        }

        private static async Task RunTest_AndrewKM_SearchAPI(Kernel kernel)
        {
            await kernel.ImportPluginFromOpenApiAsync(
               pluginName: "andrew_km",
               uri: new Uri("http://localhost:9001/swagger/v1/swagger.json"),
               executionParameters: new OpenApiFunctionExecutionParameters()
               {
                   // Determines whether payload parameter names are augmented with namespaces.
                   // Namespaces prevent naming conflicts by adding the parent parameter name
                   // as a prefix, separated by dots
                   EnablePayloadNamespacing = true,
                   HttpClient = HttpLogger.GetHttpClient(true),
                   AuthCallback = (request, cancel) =>
                   {
                       // Add the authorization header to the request
                       request.Headers.Add("Authorization", KERNEL_MEMORY_APIKEY);
                       return Task.CompletedTask;
                   },
               }
            );



            var settings = new PromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            Console.WriteLine(await kernel.InvokePromptAsync<string>(
                """
                <message role="system">
                    你是個 QA 測試人員
                    請依序執行下列步驟，並且用測試報告的格式輸出。
                    若最終符合預期結果，請傳回 "測試通過"。

                    我期待測是要能涵蓋這些期待，請產生對應的情境，確保涵蓋這些預期的結果:
                    1. 要能驗證參數的邊界範圍。例如 search( limit ), 必須是介於 1 ~ 5 的 integer, 而 index 在這邊必須是固定值 "blog"。傳回的資料數量必須小於或等於 limit 的數值。
                    2. 相同的查詢必須傳回相同數量的結果與內容

                </message>
                <message role="user">
                    測試的情境:

                    step 1, 搜尋 index: blog, "andrew"
                    step 2, 搜尋 index: blog, "sdk design"
                    step 3, 兩次搜尋至少都有一筆以上的資料回傳。
                </message>
                <message role="user">
                    請重複數次主要情境，確保有涵蓋到邊界內正確的案例，與超出邊界 (+-1) 上下限的測試案例。
                    報告的格式請列出:

                    ## 測試編號 (預期說明)
                    - 步驟 + 參數
                      (呼叫資訊: function_name( parameters ): return result 摘要 )
                      (預期結果 / 實際結果)

                    ##(測試通過 / 測試失敗)
                </message>
                """,
                new(settings)));
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

            HttpClient hc = HttpLogger.GetHttpClient(true);
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

                Console.WriteLine($"code:  {nvs["code"]}");
                Console.WriteLine($"state: {nvs["state"]}");

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
                //success = true;
                return UserAccessToken;
            }

            return null;
        }

    }
}
