using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;


using Microsoft.SemanticKernel.Plugins.OpenApi;

namespace OpenAPI
{
    internal class Program
    {
        private static string OPENAI_APIKEY = null;
        private static string OPENAI_ORGID = null;



        static async Task Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            OPENAI_APIKEY = config["OpenAI:ApiKey"];
            OPENAI_ORGID = config["OpenAI:OrgId"];

            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: "gpt-4o-mini",
                    apiKey: OPENAI_APIKEY,
                    httpClient: HttpLogger.GetHttpClient(true));

            var kernel = builder.Build();
#pragma warning disable SKEXP0040 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            HttpClient _hc = new HttpClient();
            _hc.DefaultRequestHeaders.Add("Authorization", "1234567890.1234567890.1234567890");


            await kernel.ImportPluginFromOpenApiAsync(
               //pluginName: "andrew_shop",
               //uri: new Uri("https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json"),
               pluginName: "andrew_km",
               uri: new Uri("http://localhost:9001/swagger/v1/swagger.json"),               
               executionParameters: new OpenApiFunctionExecutionParameters()
               {
                   // Determines whether payload parameter names are augmented with namespaces.
                   // Namespaces prevent naming conflicts by adding the parent parameter name
                   // as a prefix, separated by dots
                   EnablePayloadNamespacing = true,
                   HttpClient = _hc,
               }
            );
#pragma warning restore SKEXP0040 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.



            var settings = new PromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            Console.WriteLine(await kernel.InvokePromptAsync<string>(
                //"""
                //step 1, 清空購物車
                //step 2, 按照下列清單加入購物車:
                //- pid:1, 1件
                //- pid:2, 2件
                //- pid:3, 3件
                //step 3, 移除 pid:2, 1件
                //step 4, 檢查購物車內容, 是否符合:
                //- pid:1, 1件
                //- pid:2, 1件
                //- pid:3, 3件
                //""",
                """
                <message role="system">
                    你是個 QA 測試人員
                    請依序執行下列步驟，並且用測試報告的格式輸出。
                    若最終符合預期結果，請傳回 "測試通過"。
                </message>
                <message role="user">
                    step 1, 搜尋 index: blog, "andrew"
                    step 2, 搜尋 index: blog, "sdk design"
                    step 3, 兩次搜尋至少都有一筆以上的資料回傳。
                </message>
                """,
                new(settings)));

        }
    }
}
