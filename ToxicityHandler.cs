using System.Net;
using System.Text;
using System.Web;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class ToxicityHandler {
    private static readonly HttpClient httpClient = new();

    public async Task handleMessage(DiscordClient client, DiscordMessage message) {
        if (string.IsNullOrEmpty(message.Content)) {
            return;
        }

        var values = await ToxicityValues.constructToxicityValues(httpClient, message);

        if (values == null) {
            // fuckup
            return;
        }

        var toxic = false;
        var attack = false;
        var threat = false;

        if (values.toxicityScore > 0.8 || values.severeToxicityScore > 0.7) {
            toxic = true;
        }


        if (values.attackScore > 0.8 || values.insultScore > 0.8) {
            attack = true;
        }


        if (values.threatScore > 0.8) {
            threat = true;
        }


        // if it is hostile, don't process flirting
        if (threat) {
            await message.RespondAsync("This user glows");
            return;
        }

        if (attack) {
            await message.RespondAsync("Fuck you too!");
            return;
        }

        if (toxic) {
            await message.RespondAsync("shut up");
            return;
        }

        // postprocess false positive
        var content = message.Content;

        // don't say child porn is cute
        if (ActualFuzz.partialFuzz("loli", content) > 80 || ActualFuzz.partialFuzz("child", content) > 80) {
            values.sexualScore -= 0.5;
        }

        // don't say "cute" to every bloody sexual reference ever
        if (ActualFuzz.partialFuzz(new[] { "sex", "penis", "vagina", "cunt" }, content) > 90) {
            values.sexualScore -= 0.25;
        }

        if (values.sexualScore > 0.7) {
            await message.RespondAsync(DiscordEmoji.FromName(client, ":flushed:"));
        }

        if (values.flirtingScore > 0.7) {
            await message.RespondAsync($"cute! {DiscordEmoji.FromName(client, ":blue_heart:")}");
        }

        // they could be 0 (default value) if the second API errors but the condition of >0.7 covers that anyway
        if (values.sadness > 0.7 || values.fear > 0.8) {
            await message.RespondAsync("*hugs*");
        }
    }
}

public class ToxicityValues {
    public double toxicityScore;
    public double severeToxicityScore;
    public double attackScore;
    public double insultScore;
    public double profanityScore;
    public double threatScore;
    public double sexualScore;
    public double flirtingScore;
    public double joy;
    public double neutral;
    public double surprise;
    public double sadness;
    public double anger;
    public double disgust;
    public double fear;

    public static async Task<ToxicityValues?> constructToxicityValues(HttpClient client, DiscordMessage message) {
        var inst = new ToxicityValues();
        var json = $$"""
        {"comment": {"text": "{{HttpUtility.JavaScriptStringEncode(message.Content)}}" },"requestedAttributes":{
            "TOXICITY": {},
            "SEVERE_TOXICITY": {},
            "IDENTITY_ATTACK": {},
            "INSULT": {},
            "PROFANITY": { },
            "THREAT": {} }, "languages": ["en", "ru"] }
        """;
        var jsonFlirt = $$"""
        {"comment": {"text": "{{HttpUtility.JavaScriptStringEncode(message.Content)}}" },"requestedAttributes":{
            "SEXUALLY_EXPLICIT": {},
            "FLIRTATION": {} }, "languages": ["en"]}
        """;
        var responseString = await JSONParser.getJSON(
            $"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={Constants.apikey}",
            json);
        var responseStringFlirt = await JSONParser.getJSON(
            $"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={Constants.apikey}",
            jsonFlirt);
        
        var responseJson = JObject.Parse(responseString);
        var responseJsonFlirt = JObject.Parse(responseStringFlirt);

        if (responseJson["message"] != null || responseJson["details"] != null) {
            await Console.Out.WriteLineAsync(responseString);
        }

        if (responseJsonFlirt["message"] != null || responseJsonFlirt["details"] != null) {
            await Console.Out.WriteLineAsync(responseStringFlirt);
        }

        JToken? attributes;
        JToken? attributesFlirt;
        try {
            attributes = responseJson["attributeScores"];
            attributesFlirt = responseJsonFlirt["attributeScores"];
            inst.toxicityScore = attributes["TOXICITY"]["summaryScore"]["value"].Value<double>();
            inst.severeToxicityScore = attributes["SEVERE_TOXICITY"]["summaryScore"]["value"].Value<double>();
            inst.attackScore = attributes["IDENTITY_ATTACK"]["summaryScore"]["value"].Value<double>();
            inst.insultScore = attributes["INSULT"]["summaryScore"]["value"].Value<double>();
            inst.profanityScore = attributes["PROFANITY"]["summaryScore"]["value"].Value<double>();
            inst.threatScore = attributes["THREAT"]["summaryScore"]["value"].Value<double>();
            inst.sexualScore = attributesFlirt["SEXUALLY_EXPLICIT"]["summaryScore"]["value"].Value<double>();
            inst.flirtingScore = attributesFlirt["FLIRTATION"]["summaryScore"]["value"].Value<double>();
        }
        catch (Exception e) {
            await Console.Out.WriteLineAsync(responseString);
            await Console.Out.WriteLineAsync(e.ToString());
            return null;
        }

        await Console.Out.WriteLineAsync(
            $"T:{inst.toxicityScore}, ST:{inst.severeToxicityScore}, A:{inst.attackScore}, I:{inst.insultScore}, P:{inst.profanityScore}, TH:{inst.threatScore}, S:{inst.sexualScore}, F:{inst.flirtingScore}");

        const string API_URL =
            "https://api-inference.huggingface.co/models/j-hartmann/emotion-english-distilroberta-base";

        var h_json = $$"""
        {
            "inputs": "{{HttpUtility.JavaScriptStringEncode(message.Content)}}",
        }
        """;
        var httpRequestMessage = new HttpRequestMessage {
            Method = HttpMethod.Post,
            RequestUri = new Uri(API_URL),
            Headers = {
                { HttpRequestHeader.Authorization.ToString(), $"Bearer {Constants.apikey_huggingface}" },
                { HttpRequestHeader.Accept.ToString(), "application/json" },
                { "X-Version", "1" }
            },
            Content = new StringContent(h_json, Encoding.UTF8, "application/json")
        };
        var h_response = await client.SendAsync(httpRequestMessage);
        var h_responseString = await h_response.Content.ReadAsStringAsync();
        JArray h_responseJson;
        var labels = new Dictionary<string, double>();
        try {
            h_responseJson = JArray.Parse(h_responseString);
        }
        catch {
            if (h_responseString.Contains("is currently loading")) {
                // do nothing
                return inst;
            }

            await Console.Out.WriteLineAsync(h_responseString);
            return inst;
        }

        try {
            attributes = h_responseJson.First;
            foreach (var element in attributes) {
                labels[element["label"].Value<string>()] = element["score"].Value<double>();
            }
        }
        catch {
            await Console.Out.WriteLineAsync(h_responseString);
            return inst;
        }

        inst.joy = labels["joy"];
        inst.neutral = labels["neutral"];
        inst.surprise = labels["surprise"];
        inst.sadness = labels["sadness"];
        inst.anger = labels["anger"];
        inst.disgust = labels["disgust"];
        inst.fear = labels["fear"];

        await Console.Out.WriteLineAsync(
            $"J:{inst.joy}, N:{inst.neutral}, S:{inst.surprise}, SA:{inst.sadness}, A:{inst.anger}, D:{inst.disgust}, F:{inst.fear}");
        return inst;
    }
}