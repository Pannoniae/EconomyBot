using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using NLog;

namespace EconomyBot;

public class ToxicityHandler {
    private static readonly HttpClient httpClient = new();

    private const int globalCd = 15;
    private const int categoryCd = 30;
    private const int msgCd = 45;

    private DateTime? globalCooldown = new();
    private readonly Dictionary<string, DateTime?> categoryCooldowns = new();
    private readonly Dictionary<string, DateTime?> msgCooldowns = new();

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public async Task handleMessage(DiscordClient client, DiscordMessage message) { 
        if (string.IsNullOrEmpty(message.Content)) {
            return;
        }

        // setup cooldown for user
        var user = message.Author;
        var now = DateTime.Now;

        // check global cooldown
        if (checkGlobalCooldown(now)) {
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
        if (threat && !checkMsgCooldown(now, "threat")) {
            await message.RespondAsync("This user glows");
            return;
        }

        if (attack && !checkMsgCooldown(now, "attack")) {
            await message.RespondAsync("Fuck you too!");
            return;
        }

        if (toxic && !checkMsgCooldown(now, "toxic")) {
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

        if (values.sexualScore > 0.7 && !checkMsgCooldown(now, "sexual")) {
            await message.RespondAsync(DiscordEmoji.FromName(client, ":flushed:"));
        }

        if (values.flirtingScore > 0.7 && !checkMsgCooldown(now, "love")) {
            await message.RespondAsync($"cute! {DiscordEmoji.FromName(client, ":blue_heart:")}");
        }

        // they could be 0 (default value) if the second API errors but the condition of >0.7 covers that anyway
        if ((values.sadness > 0.7 || values.fear > 0.8) && !checkMsgCooldown(now, "fear")) {
            await message.RespondAsync("*hugs*");
        }
    }

    public bool checkGlobalCooldown(DateTime now) {
        if (globalCooldown != null && now - globalCooldown < TimeSpan.FromSeconds(globalCd)) {
            logger.Info($"Hit global cooldown ({now - globalCooldown})");
            globalCooldown = now;
            return true;
        }
        globalCooldown = now;
        return false;
    }
    
    public bool checkCategoryCooldown(DateTime now, string category) {
        var cooldown = categoryCooldowns.GetValueOrDefault(category);
        if (cooldown != null && now - cooldown < TimeSpan.FromSeconds(categoryCd)) {
            logger.Info($"Hit {category} category cooldown ({now - globalCooldown})");
            categoryCooldowns[category] = now;
            return true;
        }
        categoryCooldowns[category] = now;
        return false;
    }
    
    public bool checkMsgCooldown(DateTime now, string msg) {
        var cooldown = msgCooldowns.GetValueOrDefault(msg);
        if (cooldown != null && now - cooldown < TimeSpan.FromSeconds(msgCd)) {
            logger.Info($"Hit {msg} msg cooldown ({now - globalCooldown})");
            msgCooldowns[msg] = now;
            return true;
        }
        msgCooldowns[msg] = now;
        return false;
    }
}

public static class DictionaryExtensions {
    /// <summary>
    /// Adds a key/value pair to the dictionary by using the specified function
    /// if the key does not already exist. Returns the new value, or the
    /// existing value if the key exists.
    /// </summary>
    public static TValue GetOrAdd<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, TValue> valueFactory) where TKey : notnull {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(valueFactory);

        ref TValue value = ref CollectionsMarshal
            .GetValueRefOrAddDefault(dictionary, key, out bool exists)!;
        if (exists) return value;
        try {
            value = valueFactory(key);
        }
        catch {
            dictionary.Remove(key);
            throw;
        }

        return value;
    }
}

public class ToxicityValues {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
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
            logger.Warn(responseString);
        }

        if (responseJsonFlirt["message"] != null || responseJsonFlirt["details"] != null) {
            logger.Warn(responseStringFlirt);
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
            logger.Error(responseString);
            logger.Error(e.ToString());
            return null;
        }

        logger.Info(
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

            logger.Error(h_responseString);
            return inst;
        }

        try {
            attributes = h_responseJson.First;
            foreach (var element in attributes) {
                labels[element["label"].Value<string>()] = element["score"].Value<double>();
            }
        }
        catch {
            logger.Error(h_responseString);
            return inst;
        }

        inst.joy = labels["joy"];
        inst.neutral = labels["neutral"];
        inst.surprise = labels["surprise"];
        inst.sadness = labels["sadness"];
        inst.anger = labels["anger"];
        inst.disgust = labels["disgust"];
        inst.fear = labels["fear"];

        logger.Info(
            $"J:{inst.joy}, N:{inst.neutral}, S:{inst.surprise}, SA:{inst.sadness}, A:{inst.anger}, D:{inst.disgust}, F:{inst.fear}");
        return inst;
    }
}