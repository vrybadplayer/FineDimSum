using System.Text.Json.Nodes;

namespace FineDimSum.Services;


public class ReCaptchaService
{
    public static async Task<bool> verifyReCaptchaV2(string response, string secret)
    {
        using (var client = new HttpClient())
        {
            string url = "https://www.google.com/recaptcha/api/siteverify";

            MultipartFormDataContent content = new();
            content.Add(new StringContent(response), "response");
            content.Add(new StringContent(secret), "secret");

            var result = await client.PostAsync(url, content);

            if (result.IsSuccessStatusCode) 
            { 
                var strResponse = await result.Content.ReadAsStringAsync();
                Console.WriteLine(strResponse);

                var jsonResponse = JsonNode.Parse(strResponse);
                if (jsonResponse != null)
                {
                    var success = ((bool?)jsonResponse["success"]);
                    if (success != null && success == true) return true;
                }
            }
        }

        return false;
    }
}
