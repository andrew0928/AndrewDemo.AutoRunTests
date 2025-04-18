using System.ComponentModel;
using System.Text;
using MCPSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;

namespace OpenAPI_MCP
{
    internal class Program
    {
        private static string OPENAI_APIKEY;

        static async Task Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            OPENAI_APIKEY = config["OpenAI:ApiKey"];

            var plugins = await OpenApiKernelPluginFactory.CreateFromOpenApiAsync(
                "swagger",
                new Uri("https://andrewshopoauthdemo.azurewebsites.net/swagger/v1/swagger.json"));

            //MCPServer.Register<CustomSynthesisSearchPlugin>();
            await MCPSharp.MCPServer.StartAsync("DemoServer", "0.1.0");
        }









        public class CustomSynthesisSearchPlugin
        {
            public enum SynthesisTypeEnum
            {
                [Description("內容合成: Abstract, 摘要資訊")]
                Abstract,
                [Description("內容合成: Question, 將敘述內容摘要成問答形式 (FAQ), 有利於針對問題(Question)或是答案的精準檢索要求")]
                Question,
                [Description("內容合成: Problem Solving, 將敘述內容摘要成問題解決的形式，歸納成問題(Problem),原因(RootCause),解決方案(Resolution),案例(Example)的形式，有利於針對問題(Problem)的檢索，或是針對特定解決方案(Resolution)的檢索要求。")]
                Problem,
                [Description("內容合成: None, 沒有經過合成處理，直接檢索原始內容時適用")]
                None,
            }

            [McpTool("search", "Search Andrew's blog for the given query. Andrew is Microsoft MVP, good in .NET and AI application development.")]
            //[KernelFunction("search")]
            //[Description("Search Andrew's blog for the given query. Andrew is Microsoft MVP, good in .NET and AI application development.")]
            public async Task<string> AndrewBlogSearchResultAsync(
                //[Description("The query to search for.")] 
                [McpParameter(true, "The query to search for.")]
                string query,

                //[Description("Search from which synthesis source? abstract | question | problem | none")]
                //[McpParameter(true, "Search from which synthesis source? abstract | question | problem | none")]
                //SynthesisTypeEnum synthesis = SynthesisTypeEnum.None,

                //[Description("The index to search in.")]
                [McpParameter(true, "The index to search in.")]
                int limit = 3)
            {
                var synthesis = SynthesisTypeEnum.None;

                var km = new MemoryWebClient("http://127.0.0.1:9001/", KERNEL_MEMORY_APIKEY);
                var result = await km.SearchAsync(
                    query,
                    index: "blog",
                    //filter: (new MemoryFilter()).ByTag("synthesis", synthesis.ToString().ToLower()),
                    limit: limit);

                StringBuilder sb = new StringBuilder();
                foreach (var item in result.Results)
                {
                    foreach (var p in item.Partitions)
                    {
                        sb.AppendLine("".PadRight(80, '='));
                        sb.AppendLine($"## Fact:");
                        sb.AppendLine();
                        sb.AppendLine($" - Relevance: {p.Relevance}%");
                        sb.AppendLine($" - Title:     {p.Tags["post-title"][0]}");
                        sb.AppendLine($" - URL:       {p.Tags["post-url"][0]}");

                        switch (synthesis)
                        {
                            case SynthesisTypeEnum.Abstract:
                                sb.AppendLine($" - Format:    Abstract");
                                break;
                            case SynthesisTypeEnum.Question:
                                sb.AppendLine($" - Format:    FAQ");
                                break;
                            case SynthesisTypeEnum.Problem:
                                sb.AppendLine($" - Format:    Problem Solving");
                                break;
                            case SynthesisTypeEnum.None:
                            default:
                                sb.AppendLine($" - Format:    Original Content");
                                break;
                        }

                        sb.AppendLine();
                        sb.AppendLine($"```");
                        sb.AppendLine(p.Text);
                        sb.AppendLine($"```");
                        sb.AppendLine();
                    }
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Kernel Memory Search Results:");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(sb.ToString());
                Console.ResetColor();

                return sb.ToString();
            }
        }
    }
}
