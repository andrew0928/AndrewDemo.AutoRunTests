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

            // 將 APIExecutionContextPlugin 加入到 kernel 中 ( 提供 AI 可用的 tool )
            kernel.Plugins.AddFromType<APIExecutionContextPlugin>();

            // 將待測的 API ( via swagger ) 轉成 Plugin 加入到 kernel 中 ( 提供 AI 可用的 tool )
            await kernel.ImportPluginFromOpenApiAsync(
               pluginName: "andrew_shop",
               uri: new Uri("https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json"),
               executionParameters: new OpenApiFunctionExecutionParameters()
               {
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

            // 只是掃描測試案例的目錄，找到 testcase.md 就丟給 AI 執行測試, 結果會寫到同目錄的 report.md, 如果有已經存在的 report.md 則會被覆蓋
            foreach (var file in Directory.GetFiles(@"C:\CodeWork\github.com\AndrewDemo.AutoRunTests\OpenAPI\test-cases", "testcase.md", SearchOption.AllDirectories)
                .Where(x => (new FileInfo(x)).Directory.Name.StartsWith("tc-05")))
            {
                Console.WriteLine($"Run test case: {Path.GetDirectoryName(file)} ...");
                await RunTest_AndrewShop_Carts(kernel, file);
            }

            //await RunTest_AndrewShop_Carts(kernel, @"C:\CodeWork\github.com\AndrewDemo.AutoRunTests\OpenAPI\test-cases\tc-05\testcase.md");
            //await RunTest_AndrewKM_SearchAPI(kernel);
        }

        private static async Task RunTest_AndrewShop_Carts(Kernel kernel, string testcase_source)
        {
            string testcase_report = Path.Combine(Path.GetDirectoryName(testcase_source), "report.md");

            if (File.Exists(testcase_report))
            {
                File.Delete(testcase_report);
            }

            var settings = new PromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var test_case = File.ReadAllText(testcase_source);

            //
            // 直接將文字敘述的測試案例交給 AI 自動執行，並產生測試報告
            //
            var report = await kernel.InvokePromptAsync<string>(
                """
                <message role="system">
                依照我給你的 test case 執行測試。
                測試案例分為四個部分:
                - Context:  此為 API 執行的環境設置, 包含 shop id, user access token, location id 等等。請用 ApiExecutionContextPlugin 來設置這些環境變數。
                - Given:    執行測試的前置作業。進行測試前請先完成這些步驟。若 Given 的步驟執行失敗，請標記該測試 [無法執行]
                - When:     測試步驟，請按照敘述執行，呼叫指定 API 並偵測回傳結果。若這些步驟無法完成，請標記該測試 [執行失敗], 並且附上失敗的原因說明。
                - Then:     預期結果，請檢查這些步驟的執行結果是否符合預期。若這些步驟無法完成，請標記該測試 [測試不過], 並且附上不符合預期的原因說明。如果測試符合預期結果，請標記該測試 [測試通過]

                所有標示 api 的 request / response 內容, 請勿直接生成, 或是啟用任何 cache 機制替代直接呼叫 api. 我只接受真正呼叫 api 取得的 response.
                </message>
                <message role="user">
                以下為要執行的測試案例
                --
                {{$test_case}}

                </message>
                <message role="user">
                    
                生成 markdown 格式的測試報告，要包含下列資訊:

                # 測試案例名稱 (例如: TC-05, 非法上界)

                ## 測試步驟

                **Context**:

                (列出目前 context 相關設定內容)

                **Given**:

                |步驟名稱 | API | Request | Response | 測試結果 | 測試說明 |
                |---------|-----|---------|----------|----------|----------|
                | step 1  | api-name | {} | {} | pass/fail | 測試執行結果說明 |
                | step 2  | api-name | {} | {} | pass/fail | 測試執行結果說明 |
                | step 3  | api-name | {} | {} | pass/fail | 測試執行結果說明 |


                **When**:

                |步驟名稱 | API | Request | Response | 測試結果 | 測試說明 |
                |---------|-----|---------|----------|----------|----------|
                | step 1  | api-name | {} | {} | pass/fail | 測試執行結果說明 |
                | step 2  | api-name | {} | {} | pass/fail | 測試執行結果說明 |
                | step 3  | api-name | {} | {} | pass/fail | 測試執行結果說明 |
                    
                **Then**:

                - (預期結果1): 測試結果說明
                - (預期結果2): 測試結果說明
                - (預期結果3): 測試結果說明
                


                ## 測試結果
                
                **測試結果**: 無法執行(start_fail) | 執行失敗(exec_fail) | 測試不過(test_fail) | 測試通過(test_pass)
                    
                (文字敘述)
                    

                ## 結構化測試報告
                    
                (生成 json 格式的測試結果, 格式如下)
                    
                ```json
                {
                    "name": "測試案例名稱, 例如 TC-05(...)",
                    "result": start_fail|exec_fail|test_fail|test_pass,
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
                ```
                    
                ## 測試案例程式碼

                (生成能執行這段測試案例的 C# 程式碼, 用 xUnit 框架的結構來產生程式碼)

                ```csharp

                // your csharp code that can run test case here

                ```
                
                </message>
                """,
                new(settings)
                {
                    ["test_case"] = test_case
                });

            File.WriteAllText(testcase_report, report);

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
                return UserAccessToken;
            }

            return null;
        }
    }
}
