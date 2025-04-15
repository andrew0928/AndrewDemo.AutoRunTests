using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;


using Microsoft.SemanticKernel.Plugins.OpenApi;

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
                    apiKey: OPENAI_APIKEY,
                    httpClient: HttpLogger.GetHttpClient(false));

            var kernel = builder.Build();
#pragma warning disable SKEXP0040 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            HttpClient _hc = new HttpClient();
            _hc.DefaultRequestHeaders.Add("Authorization", KERNEL_MEMORY_APIKEY);


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

                    我期待測是要能涵蓋這些期待，請產生對應的情境，確保涵蓋這些預期的結果:
                    1. 要能驗證參數的邊界範圍。例如 search( limit ), 必須是介於 1 ~ 5 的 integer, 而 index 在這邊必須是固定值 "blog"
                    2. 相同的查詢必須傳回相同數量的結果與內容

                </message>
                <message role="user">
                    測試的情境:

                    step 1, 搜尋 index: blog, "andrew"
                    step 2, 搜尋 index: blog, "sdk design"
                    step 3, 兩次搜尋至少都有一筆以上的資料回傳。
                </message>
                <message role="user">
                    請重複數次主要情境，確保有涵蓋到邊界內，與超出邊界 (+-1) 的情境。
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
}
