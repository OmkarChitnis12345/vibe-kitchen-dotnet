using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/recipe/generate", async (IHttpClientFactory clientFactory, IConfiguration config, HttpContext context) =>
{
    Console.WriteLine("\n[VIBE KITCHEN] Received incoming recipe generation request...");

    try
    {
        var form = await context.Request.ReadFormAsync();
        var mood = form["mood"].ToString();
        var file = form.Files["image"];

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "Please provide an image of your fridge contents!" });
        }

        if (string.IsNullOrEmpty(mood))
        {
            mood = "Hungry and creative";
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var imageBytes = memoryStream.ToArray();
        var base64Image = Convert.ToBase64String(imageBytes);
        var mimeType = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;

        var apiKey = config["GeminiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return Results.StatusCode(500);
        }

        var promptText = $"You are an elite, witty chef who hates food waste. Look at this image of my fridge contents. The user's current mood is: '{mood}'. Generate a creative, step-by-step recipe utilizing these ingredients, add a funny chef's remark, and format it cleanly with Title, Ingredients, and Instructions.";

        var payload = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = promptText
                        },
                        new JsonObject
                        {
                            ["inline_data"] = new JsonObject
                            {
                                ["mime_type"] = mimeType,
                                ["data"] = base64Image
                            }
                        }
                    }
                }
            }
        };

        var client = clientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // Updated valid active flash models for 2026
        string[] fallbackModels = = { "gemini-2.5-flash", "gemini-2.0-flash" };
        HttpResponseMessage response = null;
        string responseString = "";
        bool success = false;

        foreach (var model in fallbackModels)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");
            
            Console.WriteLine($"[VIBE KITCHEN] Trying model: {model}...");
            response = await client.PostAsync(url, content);
            responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                success = true;
                Console.WriteLine($"[VIBE KITCHEN] Successfully generated using {model}!");
                break;
            }
            else
            {
                Console.WriteLine($"[VIBE KITCHEN WARN] Model {model} failed with response: {responseString}");
            }
        }

        if (!success)
        {
            return Results.Problem($"AI Error: All models failed. Details: {responseString}");
        }

        var jsonResponse = JsonSerializer.Deserialize<JsonObject>(responseString);
        var recipeText = jsonResponse?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() 
                         ?? "Could not extract recipe from AI response.";

        return Results.Ok(new { recipe = recipeText });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Internal Server Error: {ex.Message}");
    }
})
.DisableAntiforgery();

app.Run();