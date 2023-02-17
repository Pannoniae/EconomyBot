using System.Text;
using System.Web;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class ToxicityHandler {
    private static readonly HttpClient httpClient = new();

    public async void handleMessage(DiscordClient client, DiscordMessage message) {
        if (string.IsNullOrEmpty(message.Content)) {
            return;
        }

        var json = $$"""
        {"comment": {"text": "{{HttpUtility.JavaScriptStringEncode(message.Content)}}" },"requestedAttributes":{
            "TOXICITY": {},
            "SEVERE_TOXICITY": {},
            "IDENTITY_ATTACK": {},
            "INSULT": {},
            "PROFANITY": { },
            "THREAT": {},
            "SEXUALLY_EXPLICIT": {},
            "FLIRTATION": {} },"languages": ["en"]}
        """;
        var response = await httpClient.PostAsync(
            $"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={Constants.apikey}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var responseString = await response.Content.ReadAsStringAsync();
        var responseJson = JObject.Parse(responseString);

        if (responseJson["message"] != null || responseJson["details"] != null) {
            await Console.Out.WriteLineAsync(responseString);
        }

        JToken? attributes;
        double toxicityScore;
        double severeToxicityScore;
        double attackScore;
        double insultScore;
        double profanityScore;
        double threatScore;
        double sexualScore;
        double flirtingScore;
        try {
            attributes = responseJson["attributeScores"];
            toxicityScore = attributes["TOXICITY"]["summaryScore"]["value"].Value<double>();
            severeToxicityScore = attributes["SEVERE_TOXICITY"]["summaryScore"]["value"].Value<double>();
            attackScore = attributes["IDENTITY_ATTACK"]["summaryScore"]["value"].Value<double>();
            insultScore = attributes["INSULT"]["summaryScore"]["value"].Value<double>();
            profanityScore = attributes["PROFANITY"]["summaryScore"]["value"].Value<double>();
            threatScore = attributes["THREAT"]["summaryScore"]["value"].Value<double>();
            sexualScore = attributes["SEXUALLY_EXPLICIT"]["summaryScore"]["value"].Value<double>();
            flirtingScore = attributes["FLIRTATION"]["summaryScore"]["value"].Value<double>();
        }
        catch {
            await Console.Out.WriteLineAsync(responseString);
            return;
        }

        await Console.Out.WriteLineAsync(
            $"T:{toxicityScore}, ST:{severeToxicityScore}, A:{attackScore}, I:{insultScore}, P:{profanityScore}, TH:{threatScore}, S:{sexualScore}, F:{flirtingScore}");

        var toxic = false;
        var attack = false;
        var threat = false;

        if (toxicityScore > 0.8 || severeToxicityScore > 0.7) {
            toxic = true;
        }


        if (attackScore > 0.8 || insultScore > 0.8) {
            attack = true;
        }


        if (threatScore > 0.8) {
            threat = true;
        }


        // if it is hostile, don't process flirting
        if (threat) {
            message.RespondAsync("This user glows");
            return;
        }

        if (attack) {
            message.RespondAsync("Fuck you too!");
            return;
        }

        if (toxic) {
            message.RespondAsync("shut up");
            return;
        }

        if (sexualScore > 0.7) {
            message.RespondAsync(DiscordEmoji.FromName(client, ":flushed:"));
        }

        if (flirtingScore > 0.7) {
            message.RespondAsync($"cute! {DiscordEmoji.FromName(client, ":blue_heart:")}");
        }
    }
}