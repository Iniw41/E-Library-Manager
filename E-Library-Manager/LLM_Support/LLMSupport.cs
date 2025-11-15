using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using OllamaSharp;

namespace E_Library_Manager.LLM_Support
{
    internal class LLMSupport
    {
        static async Task SortBooks()
        {
            var client = new HttpClient();
            var url = "http://localhost:11434/api/generate";

            var request = new
            {
                model = "llama2-7b-chat",
                prompt = "Can you sort the books based on the contents is in the each file",
                max_tokens = 150,
                temperature = 0.7,
                top_p = 0.9,
                frequency_penalty = 0,
                presence_penalty = 0,
                stream = false
            };

            string jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            string jsonResponse = await response.Content.ReadAsStringAsync();

        }
    }
}
