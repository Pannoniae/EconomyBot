using NetCord;
using NetCord.Gateway;
using NetCord.Services.Commands;

namespace EconomyBot;

/// <summary>
/// Handles an interaction with the bot
/// </summary>
public class InteractionHandler {
    public static readonly Dictionary<Message, InteractionHandler> interactions = new();
    public GatewayClient client = Program.client;

    public List<Page> content;
    public int currentPage = 0;


    private DateTime createdAt = DateTime.Now;
    private bool expired = false;

    private Func<Message, bool> messageMatcher = (m) => true;
    private Action<Message> messageCallback = (m) => { };

    private InteractionHandler(List<Page> pages) {
        content = pages;
    }


    public static InteractionHandler create(CommandContext ctx, List<Page> content) {
        // remove old ones
        prune();

        var interaction = new InteractionHandler(content);
        interactions[ctx.Message] = interaction;
        return interaction;
    }

    public static void prune() {
        foreach (var i in interactions) {
            if (i.Value.createdAt < DateTime.Now.AddMinutes(-1.5)) {
                i.Value.expired = true;
            }
        }
        foreach (var item in interactions.Where(kvp => kvp.Value.expired).ToList()) {
            interactions.Remove(item.Key);
        }
    }

    public void addMatcher(Func<Message, bool> matcher) {
        messageMatcher = matcher;
    }

    public void addMessageCallback(Action<Message> callback) {
        messageCallback = callback;
    }

    public async Task waitForMessage(Message message) {
        messageCallback(message);
    }

    public static async Task messageHandler(Message message) {
        prune();
        foreach (var interaction in interactions) {
            if (interaction.Value.messageMatcher(message)) {
                await interaction.Value.waitForMessage(message);
            }
        }
    }

    public static async Task buttonHandler(ButtonInteraction bi) {
        prune();
        foreach (var interaction in interactions) {
            if (interaction.Value.messageMatcher(bi.Message)) {
                if (bi.Message.Id == interaction.Key.Id) {
                    switch (bi.Data.CustomId) {
                        case "right":
                            interaction.Value.currentPage++;
                            break;
                        case "left":
                            interaction.Value.currentPage--;
                            break;
                    }
                    await bi.Message.ModifyAsync(action => action.Content = interaction.Value.content[interaction.Value.currentPage].Content);
                }
            }
        }
    }
}