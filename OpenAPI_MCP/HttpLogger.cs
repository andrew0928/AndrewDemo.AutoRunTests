// Copyright (c) Microsoft. All rights reserved.




namespace OpenAPI_MCP
{
    /// <summary>
    /// Logging handler you might want to use to
    /// see the HTTP traffic sent by SK to LLMs.
    /// </summary>
    public class HttpLogger : DelegatingHandler
    {
        public static HttpClient GetHttpClient(bool log = false)
        {
            return log
                ? new HttpClient(new HttpLogger(new HttpClientHandler()))
                : new HttpClient();
        }

        public HttpLogger(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Error.WriteLine("Request Body:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine(request.ToString());
            Console.Error.WriteLine();
            if (request.Content != null)
            {
                Console.WriteLine(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            Console.Error.WriteLine();
            Console.ResetColor();


            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Response Body:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine(response.ToString());
            Console.Error.WriteLine();
            if (response.Content != null)
            {
                Console.Error.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            Console.Error.WriteLine();
            Console.ResetColor();


            return response;
        }
    }
}