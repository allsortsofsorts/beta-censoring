using CensorCore.Censoring;
using System.Collections.Concurrent;
using Discord.WebSocket;
using Discord;
using Newtonsoft.Json;
using System.Reflection;

namespace BetaCensor.Core
{
    public class DiscordOverrides
    {
        public ConcurrentDictionary<string, Dictionary<string, ImageCensorOptions>> Overrides { get; } = new ConcurrentDictionary<string, Dictionary<string, ImageCensorOptions>>();
    }

    public class DiscordWorkThread
    {
        //TODO make an init to populate these things
        private static DiscordSocketClient? _client;
        private static DiscordOverrides? _discordOverrides;

        public async static void DoWork(DiscordOverrides discordOverrides)
        {

            _client = new DiscordSocketClient();
            _discordOverrides = discordOverrides;

            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string fullFilePath = Path.Combine(dir, "token.txt");
            if (!File.Exists(fullFilePath))
            {
                Console.WriteLine($"Failed to load Discord token from {fullFilePath} skipping Discord Loading");
                return;
            }
            var token = File.ReadAllText(fullFilePath);

            // Centralize the logic for commands into a separate method.
            InitCommands();

            // Login and connect.
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(Timeout.Infinite);
        }

        /// TODO figure out a better way to do this unwrapping, the issue is essentially that the ImageCensor has a field incorrectly named so this is pain
        /// <summary>
        /// CensorPreferenceWrapper is a helper to take the plugin generated JSON and pull out the fields 
        /// the server cares about.
        /// </summary>
        public class CensorConfigWrapper
        {
            public CensorPrefenceWrapper? Preferences;
        }

        public class CensorPrefenceWrapper
        {
            public Dictionary<string, ImageCensorWrapperOptions>? Covered;
            public Dictionary<string, ImageCensorWrapperOptions>? Exposed;
            public Dictionary<string, ImageCensorWrapperOptions>? OtherCensoring;

        }

        // Not sure why but all the fields on this are ever so slightly wrong
        public class ImageCensorWrapperOptions
        {
            public String? Method;
            public int? Level;
        }
        private static void InitCommands()
        {
            // Subscribe a handler to see if a message invokes a command.
            _client.MessageReceived += HandleCommandAsync;
            _client.MessageUpdated += HandleUpdateAsync;
            _client.Ready += HandleClientReady;
            return;
        }

        private static void addToDict(Dictionary<string, ImageCensorOptions> dict, string key, ImageCensorWrapperOptions value)
        {
            string method = value.Method;
            if (method == "caption")
            {
                // this doesn't seem configurable from the plugin override it to keep default behavior
                method = "caption?preferBox=true&wordWrap=false";
            }
            dict.Add(key, new ImageCensorOptions(method, value.Level));
        }


        private static async void handleForceCommand(IMessage msg, bool sendPing)
        {
            var content = msg.Content;
            var startOfJSON = content.IndexOf("{");
            var endOfJSON = content.LastIndexOf("}");
            try
            {
                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
                var preferences = JsonConvert.DeserializeObject<CensorConfigWrapper>(content.Substring(startOfJSON, endOfJSON - startOfJSON + 1), jsonSerializerSettings);
                // TODO replace with more intelligent mapping, this is miserable.
                Dictionary<string, ImageCensorOptions> preferenceOverride = new Dictionary<string, ImageCensorOptions>();
                addToDict(preferenceOverride, "COVERED_BELLY", preferences?.Preferences?.Covered?.GetValueOrDefault("Belly"));
                addToDict(preferenceOverride, "EXPOSED_BELLY", preferences?.Preferences?.Exposed?.GetValueOrDefault("Belly"));
                addToDict(preferenceOverride, "COVERED_ARMPITS", preferences?.Preferences?.Covered?.GetValueOrDefault("Pits"));
                addToDict(preferenceOverride, "EXPOSED_ARMPITS", preferences?.Preferences?.Exposed?.GetValueOrDefault("Pits"));
                addToDict(preferenceOverride, "COVERED_BREAST_F", preferences?.Preferences?.Covered?.GetValueOrDefault("Breasts"));
                addToDict(preferenceOverride, "EXPOSED_BREAST_F", preferences?.Preferences?.Exposed?.GetValueOrDefault("Breasts"));
                addToDict(preferenceOverride, "COVERED_GENITALIA_M", preferences?.Preferences?.Covered?.GetValueOrDefault("Cock"));
                addToDict(preferenceOverride, "EXPOSED_GENITALIA_M", preferences?.Preferences?.Exposed?.GetValueOrDefault("Cock"));
                addToDict(preferenceOverride, "COVERED_FEET", preferences?.Preferences?.Covered?.GetValueOrDefault("Feet"));
                addToDict(preferenceOverride, "EXPOSED_FEET", preferences?.Preferences?.Exposed?.GetValueOrDefault("Feet"));
                addToDict(preferenceOverride, "COVERED_GENITALIA_F", preferences?.Preferences?.Covered?.GetValueOrDefault("Pussy"));
                addToDict(preferenceOverride, "EXPOSED_GENITALIA_F", preferences?.Preferences?.Exposed?.GetValueOrDefault("Pussy"));
                addToDict(preferenceOverride, "COVERED_BUTTOCKS", preferences?.Preferences?.Covered?.GetValueOrDefault("Ass"));
                addToDict(preferenceOverride, "EXPOSED_BUTTOCKS", preferences?.Preferences?.Exposed?.GetValueOrDefault("Ass"));
                addToDict(preferenceOverride, "COVERED_ANUS", preferences?.Preferences?.Covered?.GetValueOrDefault("Ass"));
                addToDict(preferenceOverride, "EXPOSED_ANUS", preferences?.Preferences?.Exposed?.GetValueOrDefault("Ass"));
                addToDict(preferenceOverride, "EYES_F", preferences?.Preferences?.OtherCensoring?.GetValueOrDefault("femaleEyes"));
                addToDict(preferenceOverride, "FACE_F", preferences?.Preferences?.OtherCensoring?.GetValueOrDefault("femaleFace"));
                addToDict(preferenceOverride, "MOUTH_F", preferences?.Preferences?.OtherCensoring?.GetValueOrDefault("femaleMouth"));
                addToDict(preferenceOverride, "FACE_M", preferences?.Preferences?.OtherCensoring?.GetValueOrDefault("maleFace"));
                _discordOverrides.Overrides["censorOverride"] = preferenceOverride;
                if (sendPing)
                {
                    await msg.Channel.SendMessageAsync("Updated Preferences");
                }
            }
            catch (Exception e)
            {
                if (sendPing)
                {
                    await msg.Channel.SendMessageAsync("Error Updating Preferences");
                }
                Console.WriteLine(e.ToString());
            }
            return;
        }

        private static async Task HandleClientReady()
        {
            var channel = await _client.GetDMChannelAsync(ReadChannelFromDefaultFile());

            var pinnedMessages = await channel.GetPinnedMessagesAsync();
            Console.WriteLine(pinnedMessages.Count);
            foreach (IMessage message in pinnedMessages)
            {
                // only force command is supported ignore the rest
                if (message.Content.StartsWith("!"))
                    handleForceCommand(message, false);
            }
        }

        private static async Task HandleUpdateAsync(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            await HandleCommandAsync(after);
        }
        private static async Task HandleCommandAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            //This is the line to change if you want to give someone else control.
            if (msg.Author.Id!= 1134078604242862180) return;

            // We don't want the bot to respond to itself or other bots.
            if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot) return;
            var content = msg.Content;
            if (msg.Content.StartsWith("!"))
            {
                var split = content.Split(' ', 2);
                switch (split[0])
                {
                    case "!force-config":
                        handleForceCommand(msg, true);
                        break;
                    default:
                        // unknown command don't do anything
                        break;
                }
            }
            // on first request write down the channels name
            // use this to load pins msg.Channel.GetPinnedMessagesAsync
            WriteChannelToDefaultFile(msg.Channel.Id);
        }

        private static void WriteChannelToDefaultFile(ulong channelID)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dirPath = "AllSorts_Censor";
            string filePath = "channel";
            string fullFilePath = Path.Combine(folder, dirPath, filePath);
            if (File.Exists(fullFilePath))
            {
                return;
            }
            Directory.CreateDirectory(Path.Combine(folder, "AllSorts_Censor"));
            File.WriteAllText(fullFilePath, channelID.ToString());
        }

        private static ulong ReadChannelFromDefaultFile()
        {
            //TODO shouldn't dupe but w/e
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dirPath = "AllSorts_Censor";
            string filePath = "channel";
            string fullFilePath = Path.Combine(folder, dirPath, filePath);
            if (!File.Exists(fullFilePath))
            {
                return 0;
            }
            string text = File.ReadAllText(fullFilePath);
            return Convert.ToUInt64(text);
        }
    }
}
